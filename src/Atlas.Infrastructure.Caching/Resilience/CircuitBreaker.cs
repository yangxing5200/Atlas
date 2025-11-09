using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Caching.Resilience;

/// <summary>
/// 熔断器
/// </summary>
public class CircuitBreaker
{
    private readonly ILogger<CircuitBreaker> _logger;
    private readonly double _errorThreshold;
    private readonly TimeSpan _breakDuration;
    
    private int _failureCount;
    private int _successCount;
    private DateTime? _lastFailureTime;
    private CircuitState _state = CircuitState.Closed;

    public CircuitBreaker(
        double errorThreshold,
        TimeSpan breakDuration,
        ILogger<CircuitBreaker> logger)
    {
        _errorThreshold = errorThreshold;
        _breakDuration = breakDuration;
        _logger = logger;
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
    {
        if (_state == CircuitState.Open)
        {
            if (DateTime.UtcNow - _lastFailureTime > _breakDuration)
            {
                _state = CircuitState.HalfOpen;
                _logger.LogInformation("Circuit breaker entering half-open state");
            }
            else
            {
                throw new InvalidOperationException("Circuit breaker is open");
            }
        }

        try
        {
            var result = await action();
            OnSuccess();
            return result;
        }
        catch (Exception ex)
        {
            OnFailure();
            throw;
        }
    }

    private void OnSuccess()
    {
        _successCount++;
        
        if (_state == CircuitState.HalfOpen)
        {
            _state = CircuitState.Closed;
            _failureCount = 0;
            _logger.LogInformation("Circuit breaker closed");
        }
    }

    private void OnFailure()
    {
        _failureCount++;
        _lastFailureTime = DateTime.UtcNow;

        var totalCalls = _failureCount + _successCount;
        if (totalCalls > 10)
        {
            var errorRate = (double)_failureCount / totalCalls;
            if (errorRate > _errorThreshold)
            {
                _state = CircuitState.Open;
                _logger.LogWarning("Circuit breaker opened due to high error rate: {ErrorRate}", errorRate);
            }
        }
    }
}

public enum CircuitState
{
    Closed,
    Open,
    HalfOpen
}