using Microsoft.OpenApi.Models;

namespace WashGo.HttpApi.Endpoints.Files;

internal static class UploadOpenApi
{
    public static OpenApiOperation Media(OpenApiOperation operation) => Describe(operation, documents: false);
    public static OpenApiOperation Documents(OpenApiOperation operation) => Describe(operation, documents: true);

    private static OpenApiOperation Describe(OpenApiOperation operation, bool documents)
    {
        var schema = new OpenApiSchema
        {
            Type = "object",
            Required = new HashSet<string> { "file" },
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["file"] = new() { Type = "string", Format = "binary", Description = "Chọn file cần upload." }
            }
        };
        if (documents)
        {
            schema.Required.Add("kind");
            schema.Properties["kind"] = new() { Type = "string", Description = "BusinessLicense, Contract hoặc Sla." };
            schema.Properties["merchantId"] = new() { Type = "string", Format = "uuid", Description = "Merchant liên quan (tùy chọn)." };
        }
        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new() { Schema = schema }
            }
        };
        return operation;
    }
}
