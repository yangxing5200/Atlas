namespace Atlas.Core.IdGenerators;

/// <summary>
/// 雪花ID生成服务接口
/// </summary>
public interface IIdGenerator
{
    /// <summary>
    /// 生成下一个雪花ID
    /// </summary>
    long NextId();

    /// <summary>
    /// 批量生成雪花ID
    /// </summary>
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