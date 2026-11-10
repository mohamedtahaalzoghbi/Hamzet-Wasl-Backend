namespace HamzetWasl.Infrastructure.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "HamzetWasl";
    public string Audience { get; set; } = "HamzetWasl.Clients";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
    public int GoogleTicketMinutes { get; set; } = 10;
}

public sealed class StorageOptions
{
    public const string SectionName = "Storage";
    public string RootPath { get; set; } = "App_Data/uploads";
    public string PublicBaseUrl { get; set; } = "/media";
    public string SigningKey { get; set; } = string.Empty;
    public int SignedUrlMinutes { get; set; } = 10;
}

public sealed class OtpOptions
{
    public const string SectionName = "Otp";
    public int Length { get; set; } = 6;
    public int ExpiryMinutes { get; set; } = 10;
    public int MaxAttempts { get; set; } = 5;
    public int ResendCooldownSeconds { get; set; } = 60;
    public string HashKey { get; set; } = string.Empty;
}

public sealed class RetentionOptions
{
    public const string SectionName = "Retention";
    public int VerificationEvidenceDays { get; set; } = 90;
    public int OrganizationDocumentsDaysAfterDeletion { get; set; } = 30;
    public int SosAlertsDays { get; set; } = 30;
    public int AuditYears { get; set; } = 5;
}

public sealed class SeedOptions
{
    public const string SectionName = "Seed";
    public string? AdminEmail { get; set; }
    public string? AdminPhone { get; set; }
    public string? AdminPassword { get; set; }
    public string? AdminName { get; set; } = "مدير النظام";
}

public sealed class GoogleOptions
{
    public const string SectionName = "Google";
    public string? ClientId { get; set; }
}
