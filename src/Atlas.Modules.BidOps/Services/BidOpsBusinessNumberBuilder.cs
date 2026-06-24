namespace Atlas.Modules.BidOps.Services;

public static class BidOpsBusinessNumberBuilder
{
    private const string Digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public static string Build(string prefix, long id, DateTime timestampUtc)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("Business number prefix is required.", nameof(prefix));

        return $"{prefix.Trim().ToUpperInvariant()}-{timestampUtc:yyyyMMdd}-{ToBase36(id)}";
    }

    private static string ToBase36(long value)
    {
        var unsigned = value < 0 ? (ulong)(-(value + 1)) + 1UL : (ulong)value;
        if (unsigned == 0)
            return "0";

        Span<char> buffer = stackalloc char[13];
        var index = buffer.Length;
        while (unsigned > 0)
        {
            buffer[--index] = Digits[(int)(unsigned % 36)];
            unsigned /= 36;
        }

        return new string(buffer[index..]);
    }
}
