namespace Atlas.Core.IdGenerators;

/// <summary>
/// Snowflake ID 生成器工厂
/// </summary>
public class SnowflakeIdGeneratorFactory : IIdGeneratorFactory
{
    private readonly SnowflakeIdGenerator _generator;

    public SnowflakeIdGeneratorFactory(long workerId, long datacenterId)
    {
        _generator = new SnowflakeIdGenerator(workerId, datacenterId);
    }

    public IIdGenerator GetGenerator() => new SnowflakeIdGeneratorWrapper(_generator);

    private class SnowflakeIdGeneratorWrapper : IIdGenerator
    {
        private readonly SnowflakeIdGenerator _generator;

        public SnowflakeIdGeneratorWrapper(SnowflakeIdGenerator generator)
        {
            _generator = generator;
        }

        public long NextId() => _generator.NextId();

        public long[] NextIds(int count)
        {
            return _generator.NextIds(count);
        }
    }
}