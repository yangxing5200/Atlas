namespace Atlas.Infrastructure.Security.DataMasking;

public interface IDataMaskingService
{
    object? Mask(object? value);
}
