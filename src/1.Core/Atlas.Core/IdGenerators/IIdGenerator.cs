// Atlas.Core/IdGenerators/IIdGenerator.cs

namespace Atlas.Core.IdGenerators;

/// <summary>
/// ID 生成器接口
/// </summary>
public interface IIdGenerator
{
    /// <summary>
    /// 生成新的ID
    /// </summary>
    long NextId();
    /// <summary>
    /// 批量生产新的ID
    /// </summary>
    /// <param name="count"></param>
    /// <returns></returns>
    long[] NextIds(int count);
}

/// <summary>
/// ID 生成器工厂
/// </summary>
public interface IIdGeneratorFactory
{
    /// <summary>
    /// 获取ID生成器
    /// </summary>
    IIdGenerator GetGenerator();
}