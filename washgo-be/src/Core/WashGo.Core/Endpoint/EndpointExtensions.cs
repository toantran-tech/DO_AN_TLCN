using System.Net;
using Microsoft.AspNetCore.Http;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Core.Endpoint
{
    public static class EndpointExtensions
    {
        public static IResult CustomResult<T>(this BaseResponse<T> response) =>
            response.StatusCode switch
            {
                HttpStatusCode.OK => Results.Ok(response),
                HttpStatusCode.Created => Results.Created(string.Empty, response),
                HttpStatusCode.BadRequest => Results.BadRequest(response),
                HttpStatusCode.NotFound => Results.NotFound(response),
                HttpStatusCode.Conflict => Results.Conflict(response),
                HttpStatusCode.Unauthorized => Results.Unauthorized(),
                HttpStatusCode.Forbidden => Results.Json(response, statusCode: 403),
                HttpStatusCode.TooManyRequests => Results.Json(response, statusCode: 429),
                _ => Results.BadRequest(response)
            };

        public static IResult CustomResult<T>(this PaginatedResult<T> response) =>
            response.StatusCode switch
            {
                HttpStatusCode.OK => Results.Ok(response),
                HttpStatusCode.NotFound => Results.NotFound(response),
                HttpStatusCode.Forbidden => Results.Json(response, statusCode: 403),
                HttpStatusCode.Conflict => Results.Conflict(response),
                _ => Results.BadRequest(response)
            };
    }
}
