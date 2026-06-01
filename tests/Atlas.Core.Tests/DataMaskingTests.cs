using Atlas.Core.DataMasking;
using Atlas.Infrastructure.Security.DataMasking;
using Microsoft.Extensions.Options;

namespace Atlas.Core.Tests;

public sealed class DataMaskingTests
{
    [Fact]
    public void SensitiveValueMasker_MasksCommonSensitiveValues()
    {
        var masker = new SensitiveValueMasker();

        Assert.Equal("138****5678", masker.Mask("13812345678", MaskKind.Phone));
        Assert.Equal("z***@example.com", masker.Mask("zhangsan@example.com", MaskKind.Email));
        Assert.Equal("192.168.1.*", masker.Mask("192.168.1.10", MaskKind.IpAddress));
        Assert.Equal(SensitiveValueMasker.SecretMask, masker.Mask("token-value", MaskKind.Token));
    }

    [Fact]
    public void DataMaskingService_MasksAnnotatedProperties_WhenEnabled()
    {
        var service = CreateService(enabled: true);
        var dto = new MaskedUserDto
        {
            Phone = "13812345678",
            Email = "zhangsan@example.com",
            Name = "张三"
        };

        service.Mask(dto);

        Assert.Equal("138****5678", dto.Phone);
        Assert.Equal("z***@example.com", dto.Email);
        Assert.Equal("张三", dto.Name);
    }

    [Fact]
    public void DataMaskingService_DoesNotMask_WhenDisabled()
    {
        var service = CreateService(enabled: false);
        var dto = new MaskedUserDto
        {
            Phone = "13812345678",
            Email = "zhangsan@example.com"
        };

        service.Mask(dto);

        Assert.Equal("13812345678", dto.Phone);
        Assert.Equal("zhangsan@example.com", dto.Email);
    }

    [Fact]
    public void DataMaskingService_MasksCollections()
    {
        var service = CreateService(enabled: true);
        var users = new List<MaskedUserDto>
        {
            new() { Phone = "13812345678", Email = "one@example.com" },
            new() { Phone = "13912345678", Email = "two@example.com" }
        };

        service.Mask(users);

        Assert.Equal("138****5678", users[0].Phone);
        Assert.Equal("o***@example.com", users[0].Email);
        Assert.Equal("139****5678", users[1].Phone);
        Assert.Equal("t***@example.com", users[1].Email);
    }

    private static DataMaskingService CreateService(bool enabled)
    {
        return new DataMaskingService(
            new SensitiveValueMasker(),
            new TestOptionsMonitor<DataMaskingOptions>(new DataMaskingOptions { Enabled = enabled }));
    }

    private sealed class MaskedUserDto
    {
        [SensitiveData(MaskKind.Phone)]
        public string? Phone { get; set; }

        [SensitiveData(MaskKind.Email)]
        public string? Email { get; set; }

        public string? Name { get; set; }
    }

    private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public TestOptionsMonitor(T value)
        {
            CurrentValue = value;
        }

        public T CurrentValue { get; }

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
