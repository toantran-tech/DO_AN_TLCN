using System.Collections.Generic;
using System.Net;

namespace WashGo.Core.Domain.Shared.Bases
{
    public class BaseResponse
    {
        public bool Succeeded { get; set; }
        public string? Message { get; set; }
        public Dictionary<string, string[]> Errors { get; set; } = [];
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
    }

    public class BaseResponse<T> : BaseResponse
    {
        public object? Meta { get; set; }
        public T? Data { get; set; }
    }
}
