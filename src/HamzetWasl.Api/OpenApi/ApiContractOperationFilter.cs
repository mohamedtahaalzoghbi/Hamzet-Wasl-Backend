using System.Globalization;
using HamzetWasl.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace HamzetWasl.Api.OpenApi;

/// <summary>
/// Makes the generated OpenAPI document match the real API:
/// <list type="bullet">
/// <item>the common error responses (with the standard envelope) are documented for every operation;</item>
/// <item>public operations do not require a bearer token;</item>
/// <item>the pagination parameters carry their real bounds.</item>
/// </list>
/// </summary>
public sealed class ApiContractOperationFilter : IOperationFilter
{
    private static readonly (int Status, string Description)[] CommonErrors =
    [
        (400, "الطلب غير صحيح."),
        (401, "التوكن مفقود أو منتهي."),
        (403, "لا تملك صلاحية تنفيذ الإجراء."),
        (404, "العنصر المطلوب غير موجود."),
        (409, "تعارض في البيانات."),
        (413, "حجم الطلب أو الملف أكبر من المسموح."),
        (422, "القاعدة التجارية تمنع الإجراء."),
        (429, "تم تجاوز عدد الطلبات المسموح."),
        (500, "خطأ غير متوقع.")
    ];

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Public endpoints must not advertise a bearer token.
        var allowAnonymous = context.ApiDescription.ActionDescriptor.EndpointMetadata
            .OfType<IAllowAnonymous>()
            .Any();

        if (allowAnonymous)
        {
            // An empty list marks the operation as public: it does not inherit the
            // document level bearer requirement.
            operation.Security = [];
        }
        else
        {
            // The endpoints are protected by a policy, so the bearer requirement is added
            // explicitly (otherwise the operation would look public to the clients).
            operation.Security =
            [
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", context.Document, null)] = []
                }
            ];
        }

        // The API always answers JSON: the extra media types Swashbuckle adds by default
        // would confuse the generated clients.
        if (operation.Responses is not null)
        {
            foreach (var response in operation.Responses.Values)
            {
                response.Content?.Remove("text/plain");
            }
        }

        if (operation.RequestBody?.Content is not null)
        {
            operation.RequestBody.Content.Remove("text/json");
            operation.RequestBody.Content.Remove("application/*+json");
        }

        // The error envelope is the same for every error response.
        var errorSchema = context.SchemaGenerator.GenerateSchema(
            typeof(ApiResponse<ApiError>), context.SchemaRepository);

        foreach (var (status, description) in CommonErrors)
        {
            var key = status.ToString(CultureInfo.InvariantCulture);
            if (operation.Responses is null || operation.Responses.ContainsKey(key))
            {
                continue;
            }

            operation.Responses[key] = new OpenApiResponse
            {
                Description = description,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new() { Schema = errorSchema }
                }
            };
        }

        // The pagination parameters are clamped by the services.
        foreach (var parameter in operation.Parameters ?? [])
        {
            if (parameter.Schema is not OpenApiSchema schema)
            {
                continue;
            }

            if (parameter.Name == "pageSize")
            {
                schema.Minimum = "1";
                schema.Maximum = "100";
            }

            if (parameter.Name == "page")
            {
                schema.Minimum = "1";
            }
        }
    }
}
