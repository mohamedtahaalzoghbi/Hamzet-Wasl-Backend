using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace HamzetWasl.Api.OpenApi;

/// <summary>
/// Declares the allowed values of the enumerated fields so the clients can generate typed
/// enums. The values are the ones the SQL CHECK constraints and the domain constants use.
/// </summary>
public sealed class ApiEnumSchemaFilter : ISchemaFilter
{
    private static readonly IReadOnlyDictionary<string, string[]> Enums =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["role"] = ["disabled_user", "volunteer", "association", "company", "admin"],
            ["needType"] = ["visual", "motor", "hearing"],
            ["needTypes"] = ["visual", "motor", "hearing"],
            ["theme"] = ["system", "light", "dark", "high_contrast"],
            ["interactionMode"] = ["touch", "keyboard", "screen_reader", "voice"],
            ["notificationMode"] = ["in_app", "push", "visual_alert", "vibration"],
            ["helpMode"] = ["online_only", "online_and_field"],
            ["skillLevel"] = ["beginner", "intermediate", "advanced"],
            ["platform"] = ["facebook", "linkedin", "instagram"],
            ["visibility"] = ["public", "followers", "private"],
            ["accountStatus"] = ["active", "pending_approval", "suspended"],
            ["approvalStatus"] = ["pending", "approved", "rejected"],
            ["reviewStatus"] = ["pending", "approved", "rejected"],
            ["verificationStatus"] = ["unverified", "pending", "interview_scheduled", "approved", "rejected"],
            ["result"] = ["passed", "failed"],
            ["outcome"] = ["success", "failure"],
            ["evidenceType"] = ["national_id_front", "national_id_back", "face_photo"],
            ["documentType"] = ["commercial_register", "tax_card", "address_proof", "license"],
            ["channel"] = ["in_app", "push", "visual_alert", "vibration"],
            ["type"] = ["organization_approved", "organization_rejected", "verification_approved",
                "verification_rejected", "interview_booked", "interview_changed", "interview_cancelled",
                "account_suspended", "account_reactivated"],
            ["currentStep"] = ["email", "phone", "national_id_front", "national_id_back", "social_links",
                "face_photo", "interview", "decision", "admin_review", "completed", "reapply"]
        };

    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema concrete || concrete.Properties is null || concrete.Properties.Count == 0)
        {
            return;
        }

        foreach (var property in concrete.Properties)
        {
            if (property.Value is not OpenApiSchema propertySchema ||
                !Enums.TryGetValue(property.Key, out var values))
            {
                continue;
            }

            propertySchema.Enum = values.Select(value => (JsonNode)JsonValue.Create(value)!).ToList();
            propertySchema.Type = JsonSchemaType.String;
        }
    }
}
