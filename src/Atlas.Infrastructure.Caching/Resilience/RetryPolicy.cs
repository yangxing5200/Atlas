using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Caching.Resilience;

/// <summary>
/// 重试策略
/// </summary>
public class RetryPolicy
{
    private readonly int _maxRetries;
    private readonly TimeSpan _initialDelay;
    private readonly ILogger<RetryPolicy> _logger;

    public RetryPolicy(
        int maxRetries,
        TimeSpan initialDelay,
        ILogger<RetryPolicy> logger)
    {
        _maxRetries = maxRetries;
        _initialDelay = initialDelay;
        _logger = logger;
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
    {
        var attempt = 0;
        while (true)
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                attempt++;
                
                if (attempt >= _maxRetries)
                {
                    _logger.LogError(ex, "Failed after {Attempts} attempts", attempt);
                    throw;
                }

                var delay = CalculateDelay(attempt);
                _logger.LogWarning(ex, "Attempt {Attempt} failed, retrying in {Delay}ms", 
                    attempt, delay.TotalMilliseconds);
                
                await Task.Delay(delay);
            }
        }
    }

    private TimeSpan CalculateDelay(int attempt)
    {
        // 指数退避 + 随机抖动
        var baseDelay = _initialDelay.TotalMilliseconds * Math.Pow(2, attempt - 1);
        var jitter = new Random().Next(0, (int)(baseDelay * 0.1));
        return TimeSpan.FromMilliseconds(baseDelay + jitter);
    }
}