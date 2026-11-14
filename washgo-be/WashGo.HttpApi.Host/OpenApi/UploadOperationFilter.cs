using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace WashGo.HttpApi.Host.OpenApi;

/// <summary>Apply after Swashbuckle regenerates the schema inferred from Accepts metadata.</summary>
public sealed class UploadOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!string.Equals(context.ApiDescription.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)) return;
        var route = context.ApiDescription.RelativePath?.Trim('/');
        if (route is not ("files/images" or "files/videos" or "files/documents")) return;
        Describe(operation, documents: route == "files/documents");
    }

    private static void Describe(OpenApiOperation operation, bool documents)
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
    }
}
