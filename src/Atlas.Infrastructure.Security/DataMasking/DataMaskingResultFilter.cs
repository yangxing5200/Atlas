using Atlas.Core.DataMasking;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Security.DataMasking;

public sealed class DataMaskingResultFilter : IResultFilter
{
    private readonly IDataMaskingService _maskingService;
    private readonly IOptionsMonitor<DataMaskingOptions> _options;

    public DataMaskingResultFilter(
        IDataMaskingService maskingService,
        IOptionsMonitor<DataMaskingOptions> options)
    {
        _maskingService = maskingService ?? throw new ArgumentNullException(nameof(maskingService));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (!_options.CurrentValue.Enabled || IsDisabled(context))
            return;

        switch (context.Result)
        {
            case ObjectResult { Value: not null } objectResult when !ShouldSkipValue(objectResult.Value):
                objectResult.Value = _maskingService.Mask(objectResult.Value);
                break;
            case JsonResult { Value: not null } jsonResult when !ShouldSkipValue(jsonResult.Value):
                jsonResult.Value = _maskingService.Mask(jsonResult.Value);
                break;
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }

    private static bool IsDisabled(ResultExecutingContext context)
    {
        return context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<DisableDataMaskingAttribute>() != null;
    }

    private static bool ShouldSkipValue(object value)
    {
        return value is ProblemDetails || value is FileResult || value is Stream;
    }
}
