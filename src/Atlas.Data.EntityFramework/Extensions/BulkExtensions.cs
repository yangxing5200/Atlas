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
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Data.Common.Extensions
{
    /// <summary>
    /// EF批量操作配置选项
    /// </summary>
    public class EFBulkOptions
    {
        /// <summary>
        /// 批次大小，默认1000
        /// </summary>
        public int BatchSize { get; set; } = 1000;

        /// <summary>
        /// 是否返回插入后的ID，默认true
        /// </summary>
        public bool ReturnGeneratedIds { get; set; } = true;

        /// <summary>
        /// 空值处理策略，默认插入NULL
        /// </summary>
        public NullValueHandling NullValueHandling { get; set; } = NullValueHandling.InsertNull;
    }

    /// <summary>
    /// 空值处理策略
    /// </summary>
    public enum NullValueHandling
    {
        /// <summary>
        /// 插入NULL值
        /// </summary>
        InsertNull,

        /// <summary>
        /// 忽略空值字段（不插入该列）
        /// </summary>
        Ignore
    }

    /// <summary>
    /// 批量操作扩展方法
    /// 提供高性能的批量插入和更新功能，支持自动审计、乐观锁和事务管理
    /// </summary>
    public static class BulkExtensions
    {
        #region 批量插入

        /// <summary>
        /// 批量插入实体
        /// </summary>
        public static async Task<int> BulkInsertAsync<TEntity>(
            this DbContext context,
            IEnumerable<TEntity> entities,
            int batchSize = 1000,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            return await BulkInsertAsync(context, entities, new EFBulkOptions { BatchSize = batchSize }, cancellationToken);
        }

        /// <summary>
        /// 批量插入实体（带配置）
        /// </summary>
        public static async Task<int> BulkInsertAsync<TEntity>(
            this DbContext context,
            IEnumerable<TEntity> entities,
            EFBulkOptions options,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var currentUserId = GetCurrentUserId(context);
            return await BulkInsertAsync(context, entities, currentUserId, options, cancellationToken);
        }

        /// <summary>
        /// 批量插入实体（指定用户ID）
        /// </summary>
        public static async Task<int> BulkInsertAsync<TEntity>(
            this DbContext context,
            IEnumerable<TEntity> entities,
            long? currentUserId,
            int batchSize = 1000,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            return await BulkInsertAsync(context, entities, currentUserId,
                new EFBulkOptions { BatchSize = batchSize }, cancellationToken);
        }

        /// <summary>
        /// 批量插入实体（指定用户ID和配置）
        /// </summary>
        public static async Task<int> BulkInsertAsync<TEntity>(
            this DbContext context,
            IEnumerable<TEntity> entities,
            long? currentUserId,
            EFBulkOptions options,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var entityList = entities.ToList();
            if (entityList.Count == 0) return 0;

            return await BulkInsertDirectAsync(context, entityList, currentUserId, options, cancellationToken);
        }

        #endregion

        #region 批量更新

        /// <summary>
        /// 批量更新实体
        /// </summary>
        public static async Task<int> BulkUpdateAsync<TEntity>(
            this DbContext context,
            IEnumerable<TEntity> entities,
            Expression<Func<TEntity, object>>? updateFields = null,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (entities == null) throw new ArgumentNullException(nameof(entities));

            var currentUserId = GetCurrentUserId(context);
            return await BulkUpdateAsync(context, entities, updateFields, currentUserId, cancellationToken);
        }

        /// <summary>
        /// 批量更新实体（指定用户ID）
        /// </summary>
        public static async Task<int> BulkUpdateAsync<TEntity>(
            this DbContext context,
            IEnumerable<TEntity> entities,
            Expression<Func<TEntity, object>>? updateFields,
            long? currentUserId,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (entities == null) throw new ArgumentNullException(nameof(entities));

            var entityList = entities.ToList();
            if (entityList.Count == 0) return 0;

            return await BulkUpdateDirectAsync(context, entityList, updateFields, currentUserId,
                new EFBulkOptions(), cancellationToken);
        }

        /// <summary>
        /// 批量更新实体（带配置）
        /// </summary>
        public static async Task<int> BulkUpdateAsync<TEntity>(
            this DbContext context,
            IEnumerable<TEntity> entities,
            Expression<Func<TEntity, object>> updateFields,
            EFBulkOptions options,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var currentUserId = GetCurrentUserId(context);
            var entityList = entities.ToList();
            if (entityList.Count == 0) return 0;

            return await BulkUpdateDirectAsync(context, entityList, updateFields, currentUserId, options, cancellationToken);
        }

        /// <summary>
        /// 批量更新实体（完整参数）
        /// </summary>
        public static async Task<int> BulkUpdateAsync<TEntity>(
            this DbContext context,
            IEnumerable<TEntity> entities,
            Expression<Func<TEntity, object>> updateFields,
            long? currentUserId,
            EFBulkOptions options,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            if (options == null) throw new ArgumentNullException(nameof(options));

            var entityList = entities.ToList();
            if (entityList.Count == 0) return 0;

            return await BulkUpdateDirectAsync(context, entityList, updateFields, currentUserId, options, cancellationToken);
        }

        #endregion

        #region 内部实现：直接批量插入

        /// <summary>
        /// 直接批量插入（不依赖ChangeTracker）
        /// </summary>
        private static async Task<int> BulkInsertDirectAsync<TEntity>(
            DbContext context,
            List<TEntity> entities,
            long? currentUserId,
            EFBulkOptions options,
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
                            $"实体 {typeof(TEntity).Name} 的主键不是数据库自动生成类型，必须在插入前设置ID值。");
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
                for (int i = 0; i < entities.Count; i += options.BatchSize)
                {
                    var batch = entities.Skip(i).Take(options.BatchSize).ToList();

                    foreach (var entity in batch)
                    {
                        SetAuditFieldsForInsert(entity, currentUserId, now);
                    }

                    var sql = new StringBuilder();
                    var parameters = new List<DbParameter>();
                    var valueClauses = new List<string>();
                    int paramIndex = 0;

                    if (options.NullValueHandling == NullValueHandling.Ignore)
                    {
                        foreach (var entity in batch)
                        {
                            var entityColumns = new List<string>();
                            var entityValues = new List<string>();

                            foreach (var prop in properties)
                            {
                                var value = prop.PropertyInfo?.GetValue(entity);
                                if (value != null)
                                {
                                    entityColumns.Add($"`{prop.GetColumnName()}`");
                                    var param = connection.CreateCommand().CreateParameter();
                                    param.ParameterName = $"@p{paramIndex}";
                                    param.Value = value;
                                    parameters.Add(param);
                                    entityValues.Add($"@p{paramIndex}");
                                    paramIndex++;
                                }
                            }

                            if (entityColumns.Count > 0)
                            {
                                sql.Append($"INSERT INTO {fullTableName} (");
                                sql.Append(string.Join(", ", entityColumns));
                                sql.Append(") VALUES (");
                                sql.Append(string.Join(", ", entityValues));
                                sql.AppendLine(");");
                            }
                        }
                    }
                    else
                    {
                        var columnNames = properties.Select(p => $"`{p.GetColumnName()}`").ToList();
                        sql.Append($"INSERT INTO {fullTableName} (");
                        sql.Append(string.Join(", ", columnNames));
                        sql.Append(") VALUES ");

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
                    }

                    if (parameters.Count > 0)
                    {
                        using var command = connection.CreateCommand();
                        command.CommandText = sql.ToString();
                        foreach (var param in parameters)
                        {
                            command.Parameters.Add(param);
                        }

                        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
                        totalInserted += affected;

                        if (options.ReturnGeneratedIds && isDbGeneratedId && idProperty != null)
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
        private static async Task<int> BulkUpdateDirectAsync<TEntity>(
            DbContext context,
            List<TEntity> entities,
            Expression<Func<TEntity, object>>? updateFields,
            long? currentUserId,
            EFBulkOptions options,
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

            foreach (var entity in entities)
            {
                SetAuditFieldsForUpdate(entity, currentUserId, now);
            }

            var updatePropertyNames = new HashSet<string>();
            if (updateFields != null)
            {
                var propertyNames = ExtractPropertyNames(updateFields);
                foreach (var name in propertyNames)
                {
                    updatePropertyNames.Add(name);
                }
            }

            var updateProperties = entityType.GetProperties()
                .Where(p =>
                {
                    if (p.IsKey()) return false;
                    if (p.ValueGenerated.HasFlag(ValueGenerated.OnUpdate)) return false;

                    if (updatePropertyNames.Count > 0)
                    {
                        if (p.Name == "UpdatedAt" || p.Name == "UpdatedBy" || p.Name == "Version")
                            return true;

                        return updatePropertyNames.Contains(p.Name);
                    }

                    return true;
                })
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
                    foreach (var  param in parameters)
                    {
                        command.Parameters.Add(param);
                    }

                    var affected = await command.ExecuteNonQueryAsync(cancellationToken);

                    if (hasVersion && affected != entities.Count)
                    {
                        throw new DbUpdateConcurrencyException(
                            $"乐观锁冲突：期望更新 {entities.Count} 条记录，实际更新 {affected} 条。");
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
        /// 从表达式树中提取属性名称
        /// </summary>
        private static List<string> ExtractPropertyNames<TEntity>(Expression<Func<TEntity, object>> selector)
        {
            var propertyNames = new List<string>();

            if (selector.Body is NewExpression newExpression)
            {
                foreach (var argument in newExpression.Arguments)
                {
                    if (argument is MemberExpression memberExpression)
                    {
                        propertyNames.Add(memberExpression.Member.Name);
                    }
                }
            }
            else if (selector.Body is MemberExpression memberExpr)
            {
                propertyNames.Add(memberExpr.Member.Name);
            }
            else if (selector.Body is UnaryExpression unaryExpression)
            {
                if (unaryExpression.Operand is MemberExpression memberOperand)
                {
                    propertyNames.Add(memberOperand.Member.Name);
                }
            }

            return propertyNames;
        }

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
}