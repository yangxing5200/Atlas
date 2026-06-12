using Atlas.BackgroundTasks.Operations;

namespace Atlas.Services.Tests;

public sealed class BackgroundTaskOperationsTests
{
    [Fact]
    public void SensitiveJsonMasker_MasksSensitiveJsonFields()
    {
        var masker = new SensitiveJsonMasker();
        const string json = """
{
  "channelId": 123,
  "accessToken": "secret-token",
  "profile": {
    "mobile": "13800000000",
    "email": "ops@example.local"
  }
}
""";

        var masked = masker.MaskJson(json);

        Assert.Contains("\"channelId\": 123", masked);
        Assert.Contains("\"accessToken\": \"***\"", masked);
        Assert.Contains("\"mobile\": \"***\"", masked);
        Assert.Contains("\"email\": \"***\"", masked);
        Assert.DoesNotContain("secret-token", masked);
        Assert.DoesNotContain("13800000000", masked);
        Assert.DoesNotContain("ops@example.local", masked);
    }

    [Fact]
    public void SensitiveJsonMasker_MasksPlainTextAssignments()
    {
        var masker = new SensitiveJsonMasker();

        var masked = masker.MaskText("authorization=Bearer abc token:xyz jobId=42");

        Assert.Contains("authorization=***", masked);
        Assert.Contains("token:***", masked);
        Assert.DoesNotContain("Bearer abc", masked);
        Assert.DoesNotContain("xyz", masked);
        Assert.Contains("jobId=42", masked);
    }
}
