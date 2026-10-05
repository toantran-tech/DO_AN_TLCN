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
                HttpStatusCode.Unauthorized => Results.Unauthorized(),
                _ => Results.BadRequest(response)
            };

        public static IResult CustomResult<T>(this PaginatedResult<T> response) =>
            response.StatusCode switch
            {
                HttpStatusCode.OK => Results.Ok(response),
                _ => Results.BadRequest(response)
            };
    }
}
