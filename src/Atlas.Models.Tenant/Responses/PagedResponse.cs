using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Models.Tenant.Responses
{
    public class PagedResponse<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }

        public int Total { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }

        public bool HasNext { get; set; }
        public bool HasPrevious { get; set; }

        public IReadOnlyList<T>? Items { get; set; }

        public static PagedResponse<T> FromPagedResult(PagedResult<T> result)
        {
            return new PagedResponse<T>
            {
                Success = true,
                Total = result.Total,
                PageIndex = result.PageIndex,
                PageSize = result.PageSize,
                TotalPages = result.TotalPages,
                HasNext = result.HasNext,
                HasPrevious = result.HasPrevious,
                Items = result.Items
            };
        }
    }
}
