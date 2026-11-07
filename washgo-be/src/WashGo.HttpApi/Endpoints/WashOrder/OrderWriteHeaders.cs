using System;
using Microsoft.AspNetCore.Http;
using WashGo.Application.Contracts.Features.WashOrderContracts.Commands;

namespace WashGo.HttpApi.Endpoints.WashOrder
{
    /// <summary>Header tương đương cho trường trong body: If-Match = expectedRevision, Idempotency-Key = operationId.</summary>
    internal static class OrderWriteHeaders
    {
        public static T Apply<T>(T command, HttpContext http) where T : GuardedOrderCommand
        {
            var ifMatch = http.Request.Headers.IfMatch.ToString().Trim().Trim('"');
            if (command.ExpectedRevision == null && long.TryParse(ifMatch, out var revision))
                command.ExpectedRevision = revision;
            var key = http.Request.Headers["Idempotency-Key"].ToString().Trim();
            if (command.OperationId == null && Guid.TryParse(key, out var operationId))
                command.OperationId = operationId;
            return command;
        }
    }
}
