namespace Atlas.Core.IdGenerators;

/// <summary>
/// Snowflake ID 生成器（线程安全）。
/// </summary>
/// <remarks>
/// ID 结构为：时间戳差值 + 数据中心 ID + 工作节点 ID + 毫秒内序列号。
/// 同一个生成器实例内部通过锁保证单调递增；跨节点唯一性依赖 workerId/datacenterId 配置不重复。
/// </remarks>
public class SnowflakeIdGenerator : IIdGenerator
{
    // 各部分占用的位数
    private const int WorkerIdBits = 5;        // 机器ID位数
    private const int DatacenterIdBits = 5;    // 数据中心ID位数
    private const int SequenceBits = 12;       // 序列号位数

    // 最大值
    private const long MaxWorkerId = -1L ^ (-1L << WorkerIdBits);           // 31
    private const long MaxDatacenterId = -1L ^ (-1L << DatacenterIdBits);   // 31
    private const long MaxSequence = -1L ^ (-1L << SequenceBits);           // 4095

    // 各部分的位移
    private const int WorkerIdShift = SequenceBits;                                    // 12
    private const int DatacenterIdShift = SequenceBits + WorkerIdBits;                 // 17
    private const int TimestampLeftShift = SequenceBits + WorkerIdBits + DatacenterIdBits; // 22

    // 自定义纪元用于压缩时间戳位宽。修改该值会影响历史 ID 的解析结果，发布后不要变更。
    private const long Epoch = 1704067200000L;

    private readonly long _workerId;
    private readonly long _datacenterId;
    private long _sequence = 0L;
    private long _lastTimestamp = -1L;
    private readonly object _lock = new();

    public SnowflakeIdGenerator(long workerId, long datacenterId)
    {
        if (workerId > MaxWorkerId || workerId < 0)
            throw new ArgumentException($"Worker ID 必须在 0 到 {MaxWorkerId} 之间");

        if (datacenterId > MaxDatacenterId || datacenterId < 0)
            throw new ArgumentException($"Datacenter ID 必须在 0 到 {MaxDatacenterId} 之间");

        _workerId = workerId;
        _datacenterId = datacenterId;
    }

    /// <summary>
    /// 生成下一个ID
    /// </summary>
    public long NextId()
    {
        lock (_lock)
        {
            var timestamp = GetCurrentTimestamp();

            // 时钟回拨检测
            if (timestamp < _lastTimestamp)
            {
                throw new InvalidOperationException(
                    $"时钟回拨检测：当前时间 {timestamp} 小于上次生成ID的时间 {_lastTimestamp}");
            }

            // 同一毫秒内
            if (timestamp == _lastTimestamp)
            {
                _sequence = (_sequence + 1) & MaxSequence;

                // 单毫秒内最多生成 4096 个 ID，超过后阻塞到下一毫秒保持唯一性。
                if (_sequence == 0)
                {
                    timestamp = WaitNextMillis(_lastTimestamp);
                }
            }
            else
            {
                _sequence = 0L;
            }

            _lastTimestamp = timestamp;

            // 组装ID
            return ((timestamp - Epoch) << TimestampLeftShift)
                   | (_datacenterId << DatacenterIdShift)
                   | (_workerId << WorkerIdShift)
                   | _sequence;
        }
    }

    /// <summary>
    /// 批量生成ID
    /// </summary>
    /// <param name="count">要生成的ID数量</param>
    /// <returns>生成的ID数组</returns>
    public long[] NextIds(int count)
    {
        if (count <= 0)
            throw new ArgumentException("数量必须大于0", nameof(count));

        var ids = new long[count];

        lock (_lock)
        {
            for (int i = 0; i < count; i++)
            {
                var timestamp = GetCurrentTimestamp();

                // 时钟回拨检测
                if (timestamp < _lastTimestamp)
                {
                    throw new InvalidOperationException(
                        $"时钟回拨检测：当前时间 {timestamp} 小于上次生成ID的时间 {_lastTimestamp}");
                }

                // 同一毫秒内
                if (timestamp == _lastTimestamp)
                {
                    _sequence = (_sequence + 1) & MaxSequence;

                    // 序列号溢出，等待下一毫秒
                    if (_sequence == 0)
                    {
                        timestamp = WaitNextMillis(_lastTimestamp);
                    }
                }
                else
                {
                    _sequence = 0L;
                }

                _lastTimestamp = timestamp;

                // 组装ID
                ids[i] = ((timestamp - Epoch) << TimestampLeftShift)
                       | (_datacenterId << DatacenterIdShift)
                       | (_workerId << WorkerIdShift)
                       | _sequence;
            }
        }

        return ids;
    }

    /// <summary>
    /// 等待下一毫秒
    /// </summary>
    private long WaitNextMillis(long lastTimestamp)
    {
        var timestamp = GetCurrentTimestamp();
        while (timestamp <= lastTimestamp)
        {
            timestamp = GetCurrentTimestamp();
        }
        return timestamp;
    }

    /// <summary>
    /// 获取当前时间戳（毫秒）
    /// </summary>
    private long GetCurrentTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// 从ID中解析 UTC 时间戳。
    /// </summary>
    public static DateTime ParseTimestamp(long id)
    {
        var timestamp = (id >> TimestampLeftShift) + Epoch;
        return DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime;
    }

    /// <summary>
    /// 从ID中解析数据中心ID
    /// </summary>
    public static long ParseDatacenterId(long id)
    {
        return (id >> DatacenterIdShift) & MaxDatacenterId;
    }

    /// <summary>
    /// 从ID中解析机器ID
    /// </summary>
    public static long ParseWorkerId(long id)
    {
        return (id >> WorkerIdShift) & MaxWorkerId;
    }

    /// <summary>
    /// 从ID中解析序列号
    /// </summary>
    public static long ParseSequence(long id)
    {
        return id & MaxSequence;
    }
}
