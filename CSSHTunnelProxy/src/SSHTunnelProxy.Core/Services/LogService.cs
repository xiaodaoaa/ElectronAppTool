using Microsoft.Data.Sqlite;
using SSHTunnelProxy.Core.Models;
using SSHTunnelProxy.Core.Proxy;

namespace SSHTunnelProxy.Core.Services;

/// <summary>
/// 连接日志服务（SQLite）。代理服务器通过 <see cref="IConnectionSink"/> 写入，
/// 查询在后台线程执行以避免阻塞调用方。
/// </summary>
public sealed class LogService : ILogService, IConnectionSink
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public LogService(string? databasePath = null)
    {
        databasePath ??= GetDefaultDbPath();
        var dir = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();

        EnsureSchema();
    }

    private static string GetDefaultDbPath() => Path.Combine(AppPaths.Root, "logs.db");

    private void EnsureSchema()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS connection_logs (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                Timestamp   TEXT NOT NULL,
                TunnelName  TEXT NOT NULL,
                ProxyType   INTEGER NOT NULL,
                ClientEndpoint TEXT NOT NULL,
                TargetEndpoint TEXT NOT NULL,
                BytesSent   INTEGER NOT NULL,
                BytesReceived INTEGER NOT NULL,
                DurationMs  INTEGER NOT NULL,
                Status      TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_logs_timestamp ON connection_logs(Timestamp);
            """;
        cmd.ExecuteNonQuery();
    }

    public Task AddConnectionLogAsync(ConnectionLog log)
        => RecordConnectionAsync(log);

    public async Task RecordConnectionAsync(ConnectionLog log)
    {
        await _writeLock.WaitAsync();
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO connection_logs
                    (Timestamp, TunnelName, ProxyType, ClientEndpoint,
                     TargetEndpoint, BytesSent, BytesReceived, DurationMs, Status)
                VALUES ($ts, $tn, $pt, $ce, $te, $bs, $br, $dm, $st)
                """;
            cmd.Parameters.AddWithValue("$ts", log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("$tn", log.TunnelName);
            cmd.Parameters.AddWithValue("$pt", (int)log.ProxyType);
            cmd.Parameters.AddWithValue("$ce", log.ClientEndpoint);
            cmd.Parameters.AddWithValue("$te", log.TargetEndpoint);
            cmd.Parameters.AddWithValue("$bs", log.BytesSent);
            cmd.Parameters.AddWithValue("$br", log.BytesReceived);
            cmd.Parameters.AddWithValue("$dm", (long)log.Duration.TotalMilliseconds);
            cmd.Parameters.AddWithValue("$st", log.Status);
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<IList<ConnectionLog>> QueryLogsAsync(
        string? tunnelName = null,
        DateTime? from = null,
        DateTime? to = null,
        int? limit = null)
    {
        var result = new List<ConnectionLog>();

        // SQLite 连接不可跨线程，后台线程执行完整查询。
        await Task.Run(() =>
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            var sql = new System.Text.StringBuilder("""
                SELECT Timestamp, TunnelName, ProxyType, ClientEndpoint,
                       TargetEndpoint, BytesSent, BytesReceived, DurationMs, Status
                FROM connection_logs
                WHERE 1=1
                """);
            if (!string.IsNullOrEmpty(tunnelName))
            {
                sql.Append(" AND TunnelName = $tn");
                cmd.Parameters.AddWithValue("$tn", tunnelName);
            }
            if (from.HasValue)
            {
                sql.Append(" AND Timestamp >= $from");
                cmd.Parameters.AddWithValue("$from", from.Value.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            if (to.HasValue)
            {
                sql.Append(" AND Timestamp <= $to");
                cmd.Parameters.AddWithValue("$to", to.Value.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            sql.Append(" ORDER BY Timestamp DESC");
            // 限制返回条数：日志量大时避免一次性拉取数万行到内存。
            var maxRows = limit ?? 2000;
            sql.Append(" LIMIT ").Append(maxRows);
            cmd.CommandText = sql.ToString();

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var durationMs = reader.GetInt64(7);
                result.Add(new ConnectionLog
                {
                    Timestamp = DateTime.ParseExact(reader.GetString(0), "yyyy-MM-dd HH:mm:ss", null),
                    TunnelName = reader.GetString(1),
                    ProxyType = (ProxyType)reader.GetInt32(2),
                    ClientEndpoint = reader.GetString(3),
                    TargetEndpoint = reader.GetString(4),
                    BytesSent = reader.GetInt64(5),
                    BytesReceived = reader.GetInt64(6),
                    Duration = TimeSpan.FromMilliseconds(durationMs),
                    Status = reader.GetString(8),
                });
            }
        });

        return result;
    }

    public async Task CleanupOldLogsAsync(int retainDays)
    {
        await _writeLock.WaitAsync();
        try
        {
            var cutoff = DateTime.Now.AddDays(-Math.Max(0, retainDays)).ToString("yyyy-MM-dd HH:mm:ss");
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM connection_logs WHERE Timestamp < $cutoff";
            cmd.Parameters.AddWithValue("$cutoff", cutoff);
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
