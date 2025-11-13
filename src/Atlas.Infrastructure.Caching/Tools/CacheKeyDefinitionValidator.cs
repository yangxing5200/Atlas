// Tools/CacheKeyDefinitionValidator.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Atlas.Infrastructure.Caching.Core.Models;

namespace Atlas.Infrastructure.Caching.Tools
{
    /// <summary>
    /// 缓存键定义验证器
    /// </summary>
    public static class CacheKeyDefinitionValidator
    {
        public static ValidationResult ValidateAllDefinitions(Type cacheKeysType)
        {
            var result = new ValidationResult();

            var definitions = cacheKeysType
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(CacheKeyDefinition))
                .Select(f => new
                {
                    FieldName = f.Name,
                    Definition = (CacheKeyDefinition)f.GetValue(null)!
                })
                .ToList();

            result.TotalDefinitions = definitions.Count;

            foreach (var item in definitions)
            {
                ValidateDefinition(item.FieldName, item.Definition, result);
            }

            return result;
        }

        private static void ValidateDefinition(
            string fieldName,
            CacheKeyDefinition definition,
            ValidationResult result)
        {
            // 验证命名规范
            if (string.IsNullOrWhiteSpace(definition.Name))
            {
                result.Errors.Add($"{fieldName}: Name is empty");
            }

            // 验证过期时间合理性
            if (definition.DefaultExpiration < TimeSpan.FromSeconds(1))
            {
                result.Warnings.Add($"{fieldName}: Expiration too short ({definition.DefaultExpiration})");
            }

            if (definition.DefaultExpiration > TimeSpan.FromDays(7))
            {
                result.Warnings.Add($"{fieldName}: Expiration very long ({definition.DefaultExpiration})");
            }

            // 验证实例键
            if (definition.Name.Contains("{") && string.IsNullOrEmpty(definition.InstanceKeyName))
            {
                result.Errors.Add($"{fieldName}: Name contains placeholder but InstanceKeyName is not set");
            }

            // 验证标签生成器
            if (definition.TagGenerator == null)
            {
                result.Warnings.Add($"{fieldName}: No TagGenerator defined");
            }

            // 验证描述
            if (string.IsNullOrWhiteSpace(definition.Description))
            {
                result.Warnings.Add($"{fieldName}: No Description provided");
            }
        }

        public class ValidationResult
        {
            public int TotalDefinitions { get; set; }
            public List<string> Errors { get; } = new();
            public List<string> Warnings { get; } = new();

            public bool IsValid => !Errors.Any();

            public void PrintReport()
            {
                Console.WriteLine($"Total Definitions: {TotalDefinitions}");
                Console.WriteLine($"Errors: {Errors.Count}");
                Console.WriteLine($"Warnings: {Warnings.Count}");
                Console.WriteLine();

                if (Errors.Any())
                {
                    Console.WriteLine("ERRORS:");
                    foreach (var error in Errors)
                    {
                        Console.WriteLine($"  - {error}");
                    }
                    Console.WriteLine();
                }

                if (Warnings.Any())
                {
                    Console.WriteLine("WARNINGS:");
                    foreach (var warning in Warnings)
                    {
                        Console.WriteLine($"  - {warning}");
                    }
                }
            }
        }
    }
}