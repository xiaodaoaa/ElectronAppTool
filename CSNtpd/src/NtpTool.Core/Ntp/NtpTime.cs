using System.Globalization;

namespace NtpTool.Core.Ntp;

/// <summary>
/// NTP 时间戳与 .NET <see cref="DateTime"/> 之间的转换工具。
/// NTP 时间戳为 64 位：高 32 位为自 1900-01-01T00:00:00Z 起的秒数，低 32 位为秒内小数部分。
/// </summary>
public static class NtpTime
{
    /// <summary>NTP 纪元（1900-01-01T00:00:00Z）。</summary>
    public static readonly DateTime NtpEpoch = new(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>NTP 纪元与 Unix 纪元（1970-01-01T00:00:00Z）之间的秒数差。</summary>
    public const long EpochDifferenceSeconds = 2_208_988_800L;

    /// <summary>
    /// 将 <see cref="DateTime"/> 转换为 NTP 64 位时间戳表示。
    /// 内部使用 UTC，忽略 <paramref name="utc"/> 以外的时区信息。
    /// </summary>
    public static (uint Seconds, uint Fraction) ToNtpTimestamp(DateTime utc)
    {
        DateTime utcTime = utc.Kind switch
        {
            DateTimeKind.Utc => utc,
            DateTimeKind.Local => utc.ToUniversalTime(),
            _ => DateTime.SpecifyKind(utc, DateTimeKind.Utc)
        };

        double totalSeconds = (utcTime - NtpEpoch).TotalSeconds;
        if (totalSeconds < 0)
        {
            totalSeconds = 0;
        }

        uint seconds = (uint)totalSeconds;
        double fractionDouble = totalSeconds - seconds;
        if (fractionDouble < 0)
        {
            fractionDouble = 0;
        }

        uint fraction = (uint)Math.Round(fractionDouble * uint.MaxValue);
        return (seconds, fraction);
    }

    /// <summary>
    /// 将 NTP 64 位时间戳（秒 + 小数）转换为 UTC <see cref="DateTime"/>。
    /// </summary>
    public static DateTime FromNtpTimestamp(uint seconds, uint fraction)
    {
        double doubleSeconds = seconds + (fraction / (double)uint.MaxValue);
        return NtpEpoch.AddSeconds(doubleSeconds);
    }

    /// <summary>
    /// 将 NTP 64 位原始值（大端 ulong）转换为 UTC <see cref="DateTime"/>。
    /// </summary>
    public static DateTime FromFixed64(ulong raw)
    {
        uint seconds = (uint)(raw >> 32);
        uint fraction = (uint)(raw & 0xFFFFFFFFul);
        return FromNtpTimestamp(seconds, fraction);
    }

    /// <summary>
    /// 将 UTC <see cref="DateTime"/> 转换为 NTP 64 位原始值（大端 ulong）。
    /// </summary>
    public static ulong ToFixed64(DateTime utc)
    {
        var (seconds, fraction) = ToNtpTimestamp(utc);
        return ((ulong)seconds << 32) | fraction;
    }

    /// <summary>返回 nil 时间戳对应的 NTP 时间（1900-01-01T00:00:00Z）。</summary>
    public static DateTime Zero() => NtpEpoch;

    internal static string Format(DateTime utc) => utc.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "Z";
}