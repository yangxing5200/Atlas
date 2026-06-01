namespace Atlas.Core.DataMasking;

/// <summary>
/// Marks a DTO property as sensitive so framework response masking can hide it by default.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SensitiveDataAttribute : Attribute
{
    public SensitiveDataAttribute(MaskKind kind = MaskKind.Default)
    {
        Kind = kind;
    }

    public MaskKind Kind { get; }
}
