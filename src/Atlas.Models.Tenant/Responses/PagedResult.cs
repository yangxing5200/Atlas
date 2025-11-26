using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Models.Tenant.Responses
{
    /// <summary>
    /// 分页结果
    /// </summary>
    public class PagedResult<T>
    {
        public PagedResult(long total, List<T> items, int pageIndex, int pageSize)
        {
            Total = total;
            Items = items;
            PageIndex = pageIndex;
            PageSize = pageSize;
            TotalPages = (int)Math.Ceiling(total / (double)pageSize);
        }

        /// <summary>
        /// 总记录数
        /// </summary>
        public long Total { get; set; }

        /// <summary>
        /// 当前页数据
        /// </summary>
        public List<T> Items { get; set; }

        /// <summary>
        /// 当前页码(从1开始)
        /// </summary>
        public int PageIndex { get; set; }

        /// <summary>
        /// 每页大小
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// 总页数
        /// </summary>
        public int TotalPages { get; set; }

        /// <summary>
        /// 是否有上一页
        /// </summary>
        public bool HasPrevious => PageIndex > 1;

        /// <summary>
        /// 是否有下一页
        /// </summary>
        public bool HasNext => PageIndex < TotalPages;
    }
}
