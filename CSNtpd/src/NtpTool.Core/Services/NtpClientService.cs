using NtpTool.Core.Logging;
using NtpTool.Core.Models;
using NtpTool.Core.Ntp;

namespace NtpTool.Core.Services;

/// <summary>
/// NTP 客户端服务实现。负责立即同步、定时自动同步、多服务器故障切换，
/// 并在满足条件时应用系统时间。对应需求文档第 5.2 节。
/// </summary>
public sealed class NtpClientService : INtpClientService, IDisposable
{
    private readonly NtpQueryClient _queryClient;
    private readonly ISystemTimeService _systemTime;
    private readonly IAppLogger _logger;
    private readonly ITimeApplyingStrategy _timeApplying;
    private readonly TimeProvider _timeProvider;

    private SyncScheduler _scheduler;
    private NtpClientOptions _options;
    private ClientSyncState _state;
    private NtpSyncResult? _lastResult;

    public event EventHandler<ClientSyncState>? StateChanged;
    public event EventHandler<NtpSyncResult>? SyncCompleted;

    public ClientSyncState State => _state;
    public NtpSyncResult? LastResult => _lastResult;
    public int ConsecutiveFailures { get; private set; }
    public bool IsAutoSyncRunning => _scheduler.IsRunning;

    public NtpClientService(
        NtpClientOptions options,
        ISystemTimeService systemTime,
        IAppLogger logger,
        ITimeApplyingStrategy? timeApplying = null,
        TimeProvider? timeProvider = null)
    {
        _options = options;
        _systemTime = systemTime;
        _logger = logger;
        _timeApplying = timeApplying ?? new DefaultTimeApplyingStrategy(systemTime, logger);
        _timeProvider = timeProvider ?? TimeProvider.System;
        _queryClient = new NtpQueryClient();
        _scheduler = new SyncScheduler(
            () => SyncNowAsync(CancellationToken.None),
            _timeProvider);
        SetState(ClientSyncState.Stopped);
    }

    public void ApplyOptions(NtpClientOptions options)
    {
        _options = options;
    }

    public void StartAutoSync()
    {
        if (_options.EnableAutoSync || _options.Servers.Any(s => s.Enabled))
        {
            var interval = TimeSpan.FromMinutes(Math.Max(1, _options.SyncIntervalMinutes));
            _scheduler.Start(interval);
            SetState(ClientSyncState.Idle);
            _logger.Information("NtpClient", $"已启动定时同步，周期={interval.TotalMinutes:0} 分钟。");
            return;
        }

        _logger.Warning("NtpClient", "自动同步未启动：未启用或无可用服务器。");
    }

    public void StopAutoSync()
    {
        _scheduler.Stop();
        SetState(ClientSyncState.Stopped);
        _logger.Information("NtpClient", "已停止定时同步。");
    }

    public Task<NtpSyncResult> SyncNowAsync(CancellationToken cancellationToken = default)
    {
        return SyncCoreAsync(cancellationToken);
    }

    private async Task<NtpSyncResult> SyncCoreAsync(CancellationToken cancellationToken)
    {
        SetState(ClientSyncState.Syncing);
        _logger.Information("NtpClient", "开始同步……");

        IReadOnlyList<NtpServerConfig> servers = SelectServers();
        if (servers.Count == 0)
        {
            var none = NtpSyncResult.Failed("", "没有可用的服务器。");
            none.Server = "-";
            OnComplete(none);
            return none;
        }

        NtpSyncResult? lastFailure = null;
        for (int index = 0; index < servers.Count; index++)
        {
            NtpServerConfig server = servers[index];
            cancellationToken.ThrowIfCancellationRequested();
            NtpSyncResult result;
            try
            {
                NtpExchange exchange = await _queryClient.QueryAsync(
                    server.Host, server.Port, _options.TimeoutMs, _timeProvider, cancellationToken).ConfigureAwait(false);

                result = BuildResult(server, exchange);
            }
            catch (Exception ex)
            {
                result = NtpSyncResult.Failed(Display(server), ex.Message);
            }

            if (result.Success)
            {
                OnComplete(result);
                return result;
            }

            lastFailure = result;
            _logger.Warning("NtpClient", $"服务器 {result.Server} 同步失败：{result.ErrorMessage}");

            // 重试间隔
            if (_options.RetryCount > 0 && index < servers.Count - 1)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(_options.RetryIntervalMs), _timeProvider, cancellationToken).ConfigureAwait(false);
            }
        }

        _logger.Error("NtpClient", $"所有服务器同步失败，最后一次错误：{lastFailure?.ErrorMessage}");
        OnComplete(lastFailure ?? NtpSyncResult.Failed("-", "未知错误"));
        return lastFailure ?? NtpSyncResult.Failed("-", "未知错误");
    }

    private NtpSyncResult BuildResult(NtpServerConfig server, NtpExchange exchange)
    {
        NtpPacket response = exchange.Response;
        TimeSample sample = TimeCalculator.CalculateFromResponse(response, exchange.T1, exchange.T4);

        var result = new NtpSyncResult
        {
            SyncTimeUtc = _timeProvider.GetUtcNow().UtcDateTime,
            Server = Display(server),
            Success = true,
            OffsetMs = sample.OffsetMs,
            RoundTripDelayMs = sample.RoundTripDelayMs,
            Stratum = response.Stratum,
            LeapIndicator = response.LeapIndicator,
            ReferenceId = response.ReferenceId == 0 ? null : response.ReferenceId.ToString(),
            SystemTimeChanged = false
        };

        if (!sample.IsValid(_options.MaxAcceptableDelayMs))
        {
            result.Success = false;
            result.ErrorMessage = $"往返延迟异常（{sample.RoundTripDelayMs:0.00}ms），结果不可信。";
            return result;
        }

        if (response.Stratum is 0 or >= 16)
        {
            result.Success = false;
            result.ErrorMessage = $"服务器 Stratum 无效：{response.Stratum}。";
            return result;
        }

        // 触发系统时间应用策略
        if (Math.Abs(result.OffsetMs) >= _options.AutoApplyThresholdMs)
        {
            result.SystemTimeChanged = _timeApplying.TryApply(result, _options);
        }

        _logger.Information("NtpClient",
            $"同步成功，服务器={result.Server}，offset={result.OffsetMs:+0.00;-0.00}ms，delay={result.RoundTripDelayMs:0.00}ms，stratum={result.Stratum}");

        return result;
    }

    private void OnComplete(NtpSyncResult result)
    {
        _lastResult = result;
        if (result.Success)
        {
            ConsecutiveFailures = 0;
            SetState(ClientSyncState.Success);
        }
        else
        {
            ConsecutiveFailures++;
            if (ConsecutiveFailures >= Math.Max(1, _options.FailureWarningThreshold))
            {
                SetState(ClientSyncState.Warning);
            }
            else
            {
                SetState(ClientSyncState.Failed);
            }
        }

        SyncCompleted?.Invoke(this, result);
    }

    private IReadOnlyList<NtpServerConfig> SelectServers()
    {
        return _options.Servers
            .Where(s => s.Enabled && !string.IsNullOrWhiteSpace(s.Host))
            .OrderBy(s => s.Priority)
            .ToList();
    }

    private void SetState(ClientSyncState newState)
    {
        if (_state == newState)
        {
            return;
        }

        _state = newState;
        StateChanged?.Invoke(this, newState);
    }

    private static string Display(NtpServerConfig server) =>
        string.IsNullOrWhiteSpace(server.Host) ? "-" : $"{server.Host}:{server.Port}";

    public void Dispose()
    {
        _scheduler.Dispose();
    }
}
