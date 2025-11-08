namespace Atlas.Core.Extensions;

/// <summary>
/// 日期时间扩展方法
/// </summary>
public static class DateTimeExtensions
{
    /// <summary>
    /// 转换为Unix时间戳（秒）
    /// </summary>
    public static long ToUnixTimestamp(this DateTime dateTime)
        => new DateTimeOffset(dateTime).ToUnixTimeSeconds();

    /// <summary>
    /// 转换为Unix时间戳（毫秒）
    /// </summary>
    public static long ToUnixTimestampMilliseconds(this DateTime dateTime)
        => new DateTimeOffset(dateTime).ToUnixTimeMilliseconds();

    /// <summary>
    /// 判断日期是否在指定范围内
    /// </summary>
    public static bool IsBetween(this DateTime dateTime, DateTime start, DateTime end)
        => dateTime >= start && dateTime <= end;

    /// <summary>
    /// 获取当天的开始时间（00:00:00）
    /// </summary>
    public static DateTime StartOfDay(this DateTime dateTime)
        => dateTime.Date;

    /// <summary>
    /// 获取当天的结束时间（23:59:59.999）
    /// </summary>
    public static DateTime EndOfDay(this DateTime dateTime)
        => dateTime.Date.AddDays(1).AddMilliseconds(-1);
}