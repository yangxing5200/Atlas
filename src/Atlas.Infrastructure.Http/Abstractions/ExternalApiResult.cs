namespace Atlas.Infrastructure.Http.Abstractions;

public sealed class ExternalApiResult<T>
{
    private ExternalApiResult(T? value, ExternalApiError? error)
    {
        Value = value;
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public T? Value { get; }

    public ExternalApiError? Error { get; }

    public static ExternalApiResult<T> Success(T value)
    {
        return new ExternalApiResult<T>(value, null);
    }

    public static ExternalApiResult<T> Failure(ExternalApiError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ExternalApiResult<T>(default, error);
    }
}
