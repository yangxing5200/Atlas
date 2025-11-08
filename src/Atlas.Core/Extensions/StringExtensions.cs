namespace Atlas.Core.Extensions;

/// <summary>
/// 字符串扩展方法
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// 判断字符串是否为空或null
    /// </summary>
    public static bool IsNullOrEmpty(this string? value)
        => string.IsNullOrEmpty(value);

    /// <summary>
    /// 判断字符串是否为空白或null
    /// </summary>
    public static bool IsNullOrWhiteSpace(this string? value)
        => string.IsNullOrWhiteSpace(value);

    /// <summary>
    /// 如果为null则返回空字符串
    /// </summary>
    public static string OrEmpty(this string? value)
        => value ?? string.Empty;

    /// <summary>
    /// 截断字符串到指定长度
    /// </summary>
    public static string Truncate(this string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }
}