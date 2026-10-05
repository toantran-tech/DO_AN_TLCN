using System;
using System.Collections.Generic;
using System.Net;
using Volo.Abp.Application.Services;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Core.Bases
{
    public abstract class BaseResponseHandler : ApplicationService
    {
        protected BaseResponse<T> Success<T>(T data, string? message = null)
        {
            return new BaseResponse<T>
            {
                Succeeded = true,
                StatusCode = HttpStatusCode.OK,
                Data = data,
                Message = message ?? "Thao tác thành công"
            };
        }

        protected PaginatedResult<T> Success<T>(IEnumerable<T> list, long total, int page, int pageSize, string? message = null)
        {
            int safePageSize = pageSize > 0 ? pageSize : 20;
            int totalPages = (int)Math.Ceiling((double)total / safePageSize);

            return new PaginatedResult<T>
            {
                List = list,
                Total = total,
                CurrentPage = page,
                PageSize = safePageSize,
                TotalPages = totalPages,
                StatusCode = HttpStatusCode.OK,
                Succeeded = true,
                Message = message ?? "Lấy danh sách thành công"
            };
        }

        protected BaseResponse<T> Created<T>(T data, string? message = null)
        {
            return new BaseResponse<T>
            {
                Succeeded = true,
                StatusCode = HttpStatusCode.Created,
                Data = data,
                Message = message ?? "Khởi tạo thành công"
            };
        }

        protected BaseResponse<T> BadRequest<T>(string message, Dictionary<string, string[]>? errors = null)
        {
            return new BaseResponse<T>
            {
                Succeeded = false,
                StatusCode = HttpStatusCode.BadRequest,
                Message = message,
                Errors = errors ?? []
            };
        }

        protected BaseResponse<T> NotFound<T>(string message)
        {
            return new BaseResponse<T>
            {
                Succeeded = false,
                StatusCode = HttpStatusCode.NotFound,
                Message = message
            };
        }
    }
}
