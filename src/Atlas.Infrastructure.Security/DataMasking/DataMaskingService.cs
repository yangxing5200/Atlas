using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using Atlas.Core.DataMasking;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Security.DataMasking;

public sealed class DataMaskingService : IDataMaskingService
{
    private static readonly ConcurrentDictionary<Type, MaskingProperty[]> TypePropertyCache = new();

    private readonly ISensitiveValueMasker _valueMasker;
    private readonly IOptionsMonitor<DataMaskingOptions> _options;

    public DataMaskingService(
        ISensitiveValueMasker valueMasker,
        IOptionsMonitor<DataMaskingOptions> options)
    {
        _valueMasker = valueMasker ?? throw new ArgumentNullException(nameof(valueMasker));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public object? Mask(object? value)
    {
        if (!_options.CurrentValue.Enabled || value == null)
            return value;

        MaskObject(value, new HashSet<object>(ReferenceEqualityComparer.Instance));
        return value;
    }

    private void MaskObject(object? value, ISet<object> visited)
    {
        if (value == null)
            return;

        var type = value.GetType();
        if (ShouldSkipType(type))
            return;

        if (!type.IsValueType && !visited.Add(value))
            return;

        if (value is IEnumerable enumerable && value is not string)
        {
            foreach (var element in enumerable)
            {
                MaskObject(element, visited);
            }

            return;
        }

        foreach (var property in GetMaskingProperties(type))
        {
            object? propertyValue;
            try
            {
                propertyValue = property.Property.GetValue(value);
            }
            catch
            {
                continue;
            }

            if (property.SensitiveAttribute != null)
            {
                if (property.Property.CanWrite && propertyValue is string text)
                {
                    property.Property.SetValue(value, _valueMasker.Mask(text, property.SensitiveAttribute.Kind));
                }

                continue;
            }

            MaskObject(propertyValue, visited);
        }
    }

    private static MaskingProperty[] GetMaskingProperties(Type type)
    {
        return TypePropertyCache.GetOrAdd(type, static targetType => targetType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
            .Where(property => property.CanWrite || !ShouldSkipType(property.PropertyType))
            .Select(property => new MaskingProperty(
                property,
                property.GetCustomAttribute<SensitiveDataAttribute>()))
            .Where(property => property.SensitiveAttribute != null || !ShouldSkipType(property.Property.PropertyType))
            .ToArray());
    }

    private static bool ShouldSkipType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        if (underlyingType.IsPrimitive || underlyingType.IsEnum)
            return true;

        return underlyingType == typeof(string)
               || underlyingType == typeof(decimal)
               || underlyingType == typeof(DateTime)
               || underlyingType == typeof(DateTimeOffset)
               || underlyingType == typeof(TimeSpan)
               || underlyingType == typeof(Guid)
               || typeof(ProblemDetails).IsAssignableFrom(underlyingType);
    }

    private sealed record MaskingProperty(
        PropertyInfo Property,
        SensitiveDataAttribute? SensitiveAttribute);
}
