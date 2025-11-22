using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Models.Tenant.Responses
{
    public class PagedResult<T>
    {
        public int Total { get; }
        public int PageIndex { get; }
        public int PageSize { get; }
        public int TotalPages => (int)Math.Ceiling(Total / (double)PageSize);
        public bool HasNext => PageIndex < TotalPages;
        public bool HasPrevious => PageIndex > 1;

        public IReadOnlyList<T> Items { get; }

        public PagedResult(int total, IReadOnlyList<T> items, int pageIndex, int pageSize)
        {
            Total = total;
            Items = items;
            PageIndex = pageIndex;
            PageSize = pageSize;
        }
    }
}
