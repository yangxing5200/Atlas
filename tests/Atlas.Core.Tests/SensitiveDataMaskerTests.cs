using Atlas.Infrastructure.Logging.Policies;

namespace Atlas.Core.Tests;

public sealed class SensitiveDataMaskerTests
{
    [Fact]
    public void MaskText_RedactsPasswordTokenAndSecretValues()
    {
        var masked = SensitiveDataMasker.MaskText("password=abc123 token:xyz secret=value");

        Assert.DoesNotContain("abc123", masked);
        Assert.DoesNotContain("xyz", masked);
        Assert.DoesNotContain("value", masked);
        Assert.Contains(SensitiveDataMasker.SecretMask, masked);
    }

    [Fact]
    public void MaskText_MasksPhoneAndEmailWithStablePolicy()
    {
        var masked = SensitiveDataMasker.MaskText("phone=13812345678 email=jason@example.com");

        Assert.Contains("138****5678", masked);
        Assert.Contains("j***@example.com", masked);
        Assert.DoesNotContain("13812345678", masked);
        Assert.DoesNotContain("jason@example.com", masked);
    }

    [Fact]
    public void MaskByField_UsesConfigurableSensitiveFieldNames()
    {
        var masked = SensitiveDataMasker.MaskByField("apiKey", "plain-secret");

        Assert.Equal(SensitiveDataMasker.SecretMask, masked);
    }
}
