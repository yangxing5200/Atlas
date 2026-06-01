namespace Atlas.Core.DataMasking;

public interface ISensitiveValueMasker
{
    string? Mask(string? value, MaskKind kind);

    string MaskText(string value);
}
