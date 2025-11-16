using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Atlas.Data.Tenant
{
    /// <summary>
    /// DbContext访问验证器 - 确保只能通过Repository访问
    /// </summary>
    internal static class DbContextAccessValidator
    {
        /// <summary>
        /// 验证调用者是否有权访问 GetDbSet 方法
        /// </summary>
        /// <exception cref="InvalidOperationException">非法调用时抛出</exception>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ValidateAccess()
        {
#if DEBUG
            // 只在 Debug 模式下启用检查（避免性能影响）
            var stackTrace = new StackTrace();
            var frames = stackTrace.GetFrames();

            if (frames == null || frames.Length < 3)
            {
                return;
            }

            // 检查调用栈中是否有 RepositoryBase
            bool hasRepositoryInStack = frames
                .Skip(2) // 跳过当前方法和 GetDbSet
                .Take(5) // 只检查前5层调用
                .Any(frame =>
                {
                    var method = frame.GetMethod();
                    if (method == null) return false;

                    var declaringType = method.DeclaringType;
                    if (declaringType == null) return false;

                    // 检查是否是 RepositoryBase 或其子类
                    var typeName = declaringType.Name;
                    return typeName.Contains("RepositoryBase") ||
                           (declaringType.BaseType != null &&
                            declaringType.BaseType.Name.Contains("RepositoryBase"));
                });

            if (!hasRepositoryInStack)
            {
                var caller = frames[2].GetMethod();
                var callerType = caller?.DeclaringType?.FullName ?? "Unknown";
                var callerMethod = caller?.Name ?? "Unknown";

                throw new InvalidOperationException(
                    $"禁止直接访问 AtlasTenantDbContext.GetDbSet 方法！\n" +
                    $"调用者: {callerType}.{callerMethod}\n" +
                    $"必须通过 Repository 访问数据库。\n" +
                    $"如需访问 Store 表，请注入 IStoreRepository。");
            }
#endif
        }
    }
}