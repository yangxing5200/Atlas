using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Core.Extensions
{
    public static class LinqExtensions
    {
        public static bool IsNullOrEmpty<T>(this IEnumerable<T>? source)
        {
            return source == null || !source.Any();
        }

        public static bool SafeAny<T>(this IEnumerable<T>? source)
        {
            return source != null && source.Any();
        }
        public static void SafeForEach<T>(this IEnumerable<T>? source, Action<T> action)
        {
            if (source != null)
            {
                foreach (var item in source)
                    action(item);
            }
        }
    }
}
