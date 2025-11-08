using Atlas.Core.Entities;
using Atlas.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Data.Common.Extensions
{
    /// <summary>
    /// 批量操作扩展方法
    /// 提供高性能的批量插入和更新功能，支持自动审计、乐观锁和事务管理
    /// </summary>
    public static class SmartBatchExtensions
    {
        #region 批量插入

        /// <summary>
        /// 批量插入实体
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="context">数据库上下文</param>
        /// <param name="entities">待插入的实体集合</param>
        /// <param name="batchSize">批次大小，默认1000条</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>插入的记录数</returns>
        public static async Task<int> BatchInsertAsync<TEntity>(
            this DbContext context,
            IEnumerable<TEntity> entities,
            int batchSize = 1000,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (entities == null) throw new ArgumentNullException(nameof(entities));

            var currentUserId = GetCurrentUserId(context);
            return await BatchInsertAsync(context, entities, currentUserId, batchSize, cancellationToken);
        }

        /// <summary>
        /// 批量插入实体
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="context">数据库上下文</param>
        /// <param name="entities">待插入的实体集合</param>
        /// <param name="currentUserId">当前用户ID</param>
        /// <param name="batchSize">批次大小，默认1000条</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>插入的记录数</returns>
        public static async Task<int> BatchInsertAsync<TEntity>(
            this DbContext context,
            IEnumerable<TEntity> entities,
            long? currentUserId,
            int batchSize = 1000,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (entities == null) throw new ArgumentNullException(nameof(entities));

            var entityList = entities.ToList();
            if (entityList.Count == 0) return 0;

            return await BatchInsertDirectAsync(context, entityList, currentUserId, batchSize, cancellationToken);
        }

        #endregion

        #region 批量更新

        /// <summary>
        /// 批量更新实体
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="context">数据库上下文</param>
        /// <param name="entities">待更新的实体集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>更新的记录数</returns>
        public static async Task<int> BatchUpdateAsync<TEntity>(
            this DbContext context,
            IEnumerable<TEntity> entities,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (entities == null) throw new ArgumentNullException(nameof(entities));

            var currentUserId = GetCurrentUserId(context);
            return await BatchUpdateAsync(context, entities, currentUserId, cancellationToken);
        }

        /// <summary>
        /// 批量更新实体
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="context">数据库上下文</param>
        /// <param name="entities">待更新的实体集合</param>
        /// <param name="currentUserId">当前用户ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>更新的记录数</returns>
        public static async Task<int> BatchUpdateAsync<TEntity>(
            this DbContext context,
            IEnumerable<TEntity> entities,
            long? currentUserId,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (entities == null) throw new ArgumentNullException(nameof(entities));

            var entityList = entities.ToList();
            if (entityList.Count == 0) return 0;

            return await BatchUpdateDirectAsync(context, entityList, currentUserId, cancellationToken);
        }

        #endregion

        #region 内部实现：直接批量插入

        /// <summary>
        /// 直接批量插入（不依赖ChangeTracker）
        /// </summary>
        private static async Task<int> BatchInsertDirectAsync<TEntity>(
            DbContext context,
            List<TEntity> entities,
            long? currentUserId,
            int batchSize,
            CancellationToken cancellationToken)
            where TEntity : class
        {
            var entityType = context.Model.FindEntityType(typeof(TEntity));
            var tableName = entityType!.GetTableName();
            var schema = entityType.GetSchema();
            var fullTableName = string.IsNullOrEmpty(schema) ? $"`{tableName}`" : $"`{schema}`.`{tableName}`";

            var primaryKey = entityType.FindPrimaryKey();
            var idProperty = primaryKey?.Properties.FirstOrDefault();
            var isDbGeneratedId = idProperty?.ValueGenerated == ValueGenerated.OnAdd;

            // 验证非数据库生成ID的实体必须提供ID值
            if (!isDbGeneratedId && idProperty != null)
            {
                foreach (var entity in entities)
                {
                    if (entity is BaseEntity baseEntity && baseEntity.Id == 0)
                    {
                        throw new InvalidOperationException(
                            $"实体 {typeof(TEntity).Name} 的主键不是数据库自动生成类型，必须在插入前设置ID值。" +
                            $"请确保实体的ID已被赋值，或将主键配置为数据库自增。");
                    }
                }
            }

            var properties = entityType.GetProperties()
                .Where(p =>
                {
                    if (p.Name == "Id" && isDbGeneratedId)
                        return false;
                    if (p.Name == "Version")
                        return true;
                    if (p.ValueGenerated.HasFlag(ValueGenerated.OnAdd))
                        return false;
                    return true;
                })
                .ToList();

            var now = DateTime.UtcNow;
            int totalInserted = 0;

            var connection = context.Database.GetDbConnection();
            var wasConnected = connection.State == ConnectionState.Open;

            if (!wasConnected)
                await connection.OpenAsync(cancellationToken);

            try
            {
                for (int i = 0; i < entities.Count; i += batchSize)
                {
                    var batch = entities.Skip(i).Take(batchSize).ToList();

                    // 设置审计字段
                    foreach (var entity in batch)
                    {
                        SetAuditFieldsForInsert(entity, currentUserId, now);
                    }

                    // 构建SQL
                    var sql = new StringBuilder();
                    sql.Append($"INSERT INTO {fullTableName} (");

                    var columnNames = properties.Select(p => $"`{p.GetColumnName()}`").ToList();
                    sql.Append(string.Join(", ", columnNames));
                    sql.Append(") VALUES ");

                    var parameters = new List<DbParameter>();
                    var valueClauses = new List<string>();
                    int paramIndex = 0;

                    foreach (var entity in batch)
                    {
                        var values = new List<string>();
                        foreach (var prop in properties)
                        {
                            var value = prop.PropertyInfo?.GetValue(entity);
                            var param = connection.CreateCommand().CreateParameter();
                            param.ParameterName = $"@p{paramIndex}";
                            param.Value = value ?? DBNull.Value;
                            parameters.Add(param);
                            values.Add($"@p{paramIndex}");
                            paramIndex++;
                        }
                        valueClauses.Add($"({string.Join(", ", values)})");
                    }

                    sql.Append(string.Join(", ", valueClauses));

                    // 执行SQL
                    using var command = connection.CreateCommand();
                    command.CommandText = sql.ToString();
                    foreach (var param in parameters)
                    {
                        command.Parameters.Add(param);
                    }

                    var affected = await command.ExecuteNonQueryAsync(cancellationToken);
                    totalInserted += affected;

                    // ID回填
                    if (isDbGeneratedId && idProperty != null)
                    {
                        using var cmd = connection.CreateCommand();
                        cmd.CommandText = "SELECT LAST_INSERT_ID()";
                        var lastInsertId = Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken));

                        for (int j = 0; j < batch.Count; j++)
                        {
                            var entity = batch[j];
                            var newId = lastInsertId + j;
                            idProperty.PropertyInfo?.SetValue(entity, newId);
                        }
                    }
                }

                return totalInserted;
            }
            finally
            {
                if (!wasConnected && connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }
        }

        #endregion

        #region 内部实现：直接批量更新

        /// <summary>
        /// 直接批量更新（不依赖ChangeTracker）
        /// </summary>
        private static async Task<int> BatchUpdateDirectAsync<TEntity>(
            DbContext context,
            List<TEntity> entities,
            long? currentUserId,
            CancellationToken cancellationToken)
            where TEntity : class
        {
            var entityType = context.Model.FindEntityType(typeof(TEntity));
            var tableName = entityType!.GetTableName();
            var schema = entityType.GetSchema();
            var fullTableName = string.IsNullOrEmpty(schema) ? $"`{tableName}`" : $"`{schema}`.`{tableName}`";

            var primaryKey = entityType.FindPrimaryKey();
            var idProperty = primaryKey!.Properties.First();
            var idColumnName = idProperty.GetColumnName();

            var hasVersion = typeof(TEntity).IsAssignableTo(typeof(VersionedEntity));
            var versionProperty = hasVersion
                ? entityType.GetProperties().FirstOrDefault(p => p.Name == "Version")
                : null;

            var now = DateTime.UtcNow;

            // 保存原始version（在设置审计字段之前）
            var originalVersions = new Dictionary<object, int>();
            if (hasVersion && versionProperty != null)
            {
                foreach (var entity in entities)
                {
                    var id = idProperty.PropertyInfo?.GetValue(entity);
                    var version = (int)(versionProperty.PropertyInfo?.GetValue(entity) ?? 0);
                    originalVersions[id!] = version;
                }
            }

            // 设置审计字段（会递增version）
            foreach (var entity in entities)
            {
                SetAuditFieldsForUpdate(entity, currentUserId, now);
            }

            // 获取所有非主键属性
            var updateProperties = entityType.GetProperties()
                .Where(p => !p.IsKey() && !p.ValueGenerated.HasFlag(ValueGenerated.OnUpdate))
                .ToList();

            var connection = context.Database.GetDbConnection();
            var wasConnected = connection.State == ConnectionState.Open;

            if (!wasConnected)
                await connection.OpenAsync(cancellationToken);

            try
            {
                using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

                try
                {
                    var sqlBatch = new StringBuilder();
                    var parameters = new List<DbParameter>();
                    int paramIndex = 0;

                    foreach (var entity in entities)
                    {
                        var id = idProperty.PropertyInfo?.GetValue(entity);

                        sqlBatch.Append($"UPDATE {fullTableName} SET ");

                        var setClauses = new List<string>();
                        foreach (var prop in updateProperties)
                        {
                            var value = prop.PropertyInfo?.GetValue(entity);
                            var param = connection.CreateCommand().CreateParameter();
                            param.ParameterName = $"@p{paramIndex}";
                            param.Value = value ?? DBNull.Value;
                            parameters.Add(param);

                            setClauses.Add($"`{prop.GetColumnName()}`=@p{paramIndex}");
                            paramIndex++;
                        }

                        sqlBatch.Append(string.Join(", ", setClauses));
                        sqlBatch.Append($" WHERE `{idColumnName}`={id}");

                        if (hasVersion && versionProperty != null && originalVersions.TryGetValue(id!, out var originalVersion))
                        {
                            sqlBatch.Append($" AND `{versionProperty.GetColumnName()}`={originalVersion}");
                        }

                        sqlBatch.AppendLine(";");
                    }

                    using var command = connection.CreateCommand();
                    command.Transaction = transaction.GetDbTransaction();
                    command.CommandText = sqlBatch.ToString();
                    foreach (var param in parameters)
                    {
                        command.Parameters.Add(param);
                    }

                    var affected = await command.ExecuteNonQueryAsync(cancellationToken);

                    if (hasVersion && affected != entities.Count)
                    {
                        throw new DbUpdateConcurrencyException(
                            $"乐观锁冲突：期望更新 {entities.Count} 条记录，实际更新 {affected} 条。" +
                            $"数据可能已被其他用户修改。");
                    }

                    await transaction.CommitAsync(cancellationToken);
                    return affected;
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            }
            finally
            {
                if (!wasConnected && connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 设置插入时的审计字段
        /// </summary>
        private static void SetAuditFieldsForInsert(object entity, long? userId, DateTime now)
        {
            if (entity is BaseEntity baseEntity)
            {
                baseEntity.CreatedAt = now;
            }

            if (entity is AuditableEntity auditable)
            {
                auditable.CreatedBy = userId;
            }

            if (entity is VersionedEntity versioned)
            {
                versioned.Version = 0;
            }
        }

        /// <summary>
        /// 设置更新时的审计字段
        /// </summary>
        private static void SetAuditFieldsForUpdate(object entity, long? userId, DateTime now)
        {
            if (entity is BaseEntity baseEntity)
            {
                baseEntity.UpdatedAt = now;
            }

            if (entity is AuditableEntity auditable)
            {
                auditable.UpdatedBy = userId;
            }

            if (entity is VersionedEntity versioned)
            {
                versioned.Version++;
            }
        }

        /// <summary>
        /// 从DbContext获取当前用户ID
        /// </summary>
        private static long? GetCurrentUserId(DbContext context)
        {
            if (context is IHasCurrentUser hasCurrentUser)
            {
                return hasCurrentUser.CurrentUserId;
            }
            return null;
        }

        #endregion
    }

    /// <summary>
    /// 批量保存结果
    /// </summary>
    public class BatchSaveResult
    {
        /// <summary>
        /// 插入的记录数
        /// </summary>
        public int InsertedCount { get; set; }

        /// <summary>
        /// 更新的记录数
        /// </summary>
        public int UpdatedCount { get; set; }

        /// <summary>
        /// 总操作记录数
        /// </summary>
        public int TotalCount => InsertedCount + UpdatedCount;

        public override string ToString()
        {
            return $"插入: {InsertedCount}, 更新: {UpdatedCount}, 总计: {TotalCount}";
        }
    }
}