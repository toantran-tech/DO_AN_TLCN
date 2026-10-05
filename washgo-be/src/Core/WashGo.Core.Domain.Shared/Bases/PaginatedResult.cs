using System.Collections.Generic;
using System.Net;

namespace WashGo.Core.Domain.Shared.Bases
{
    public class PaginatedResult<T>
    {
        public IEnumerable<T> List { get; set; } = [];
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public long Total { get; set; }
        public int PageSize { get; set; }
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
        public string? Message { get; set; }
        public bool Succeeded { get; set; } = true;
    }
}
