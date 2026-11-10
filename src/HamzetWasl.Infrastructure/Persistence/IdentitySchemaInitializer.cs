using Microsoft.EntityFrameworkCore;

namespace HamzetWasl.Infrastructure.Persistence;

/// <summary>
/// Creates the ASP.NET Core Identity tables in a database whose domain tables
/// were created from Hamzet_Wasl_Schema_v2.sql. Identity tables are outside the
/// authoritative 60-table schema and are intentionally initialised separately.
/// </summary>
public static class IdentitySchemaInitializer
{
    public static async Task InitializeAsync(HamzetWaslDbContext db, CancellationToken ct = default)
    {
        const string sql = """
IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetUsers (
        Id UNIQUEIDENTIFIER NOT NULL,
        UserName NVARCHAR(256) NULL,
        NormalizedUserName NVARCHAR(256) NULL,
        Email NVARCHAR(256) NULL,
        NormalizedEmail NVARCHAR(256) NULL,
        EmailConfirmed BIT NOT NULL,
        PasswordHash NVARCHAR(MAX) NULL,
        SecurityStamp NVARCHAR(MAX) NULL,
        ConcurrencyStamp NVARCHAR(MAX) NULL,
        PhoneNumber NVARCHAR(MAX) NULL,
        PhoneNumberConfirmed BIT NOT NULL,
        TwoFactorEnabled BIT NOT NULL,
        LockoutEnd DATETIMEOFFSET NULL,
        LockoutEnabled BIT NOT NULL,
        AccessFailedCount INT NOT NULL,
        CONSTRAINT PK_AspNetUsers PRIMARY KEY (Id)
    );
    CREATE UNIQUE INDEX IX_AspNetUsers_NormalizedUserName ON dbo.AspNetUsers(NormalizedUserName) WHERE NormalizedUserName IS NOT NULL;
    CREATE UNIQUE INDEX IX_AspNetUsers_NormalizedEmail ON dbo.AspNetUsers(NormalizedEmail) WHERE NormalizedEmail IS NOT NULL;
END;

IF OBJECT_ID(N'dbo.AspNetRoles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetRoles (
        Id UNIQUEIDENTIFIER NOT NULL,
        Name NVARCHAR(256) NULL,
        NormalizedName NVARCHAR(256) NULL,
        ConcurrencyStamp NVARCHAR(MAX) NULL,
        CONSTRAINT PK_AspNetRoles PRIMARY KEY (Id)
    );
    CREATE UNIQUE INDEX IX_AspNetRoles_NormalizedName ON dbo.AspNetRoles(NormalizedName) WHERE NormalizedName IS NOT NULL;
END;

IF OBJECT_ID(N'dbo.AspNetUserRoles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetUserRoles (
        UserId UNIQUEIDENTIFIER NOT NULL,
        RoleId UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT PK_AspNetUserRoles PRIMARY KEY (UserId, RoleId)
    );
    CREATE INDEX IX_AspNetUserRoles_RoleId ON dbo.AspNetUserRoles(RoleId);
END;

IF OBJECT_ID(N'dbo.AspNetUserClaims', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetUserClaims (
        Id INT IDENTITY(1,1) NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        ClaimType NVARCHAR(MAX) NULL,
        ClaimValue NVARCHAR(MAX) NULL,
        CONSTRAINT PK_AspNetUserClaims PRIMARY KEY (Id)
    );
    CREATE INDEX IX_AspNetUserClaims_UserId ON dbo.AspNetUserClaims(UserId);
END;

IF OBJECT_ID(N'dbo.AspNetRoleClaims', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetRoleClaims (
        Id INT IDENTITY(1,1) NOT NULL,
        RoleId UNIQUEIDENTIFIER NOT NULL,
        ClaimType NVARCHAR(MAX) NULL,
        ClaimValue NVARCHAR(MAX) NULL,
        CONSTRAINT PK_AspNetRoleClaims PRIMARY KEY (Id)
    );
    CREATE INDEX IX_AspNetRoleClaims_RoleId ON dbo.AspNetRoleClaims(RoleId);
END;

IF OBJECT_ID(N'dbo.AspNetUserLogins', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetUserLogins (
        LoginProvider NVARCHAR(450) NOT NULL,
        ProviderKey NVARCHAR(450) NOT NULL,
        ProviderDisplayName NVARCHAR(MAX) NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT PK_AspNetUserLogins PRIMARY KEY (LoginProvider, ProviderKey)
    );
    CREATE INDEX IX_AspNetUserLogins_UserId ON dbo.AspNetUserLogins(UserId);
END;

IF OBJECT_ID(N'dbo.AspNetUserTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetUserTokens (
        UserId UNIQUEIDENTIFIER NOT NULL,
        LoginProvider NVARCHAR(450) NOT NULL,
        Name NVARCHAR(450) NOT NULL,
        Value NVARCHAR(MAX) NULL,
        CONSTRAINT PK_AspNetUserTokens PRIMARY KEY (UserId, LoginProvider, Name)
    );
END;

IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_AspNetUsers_USER_ACCOUNT')
BEGIN
    ALTER TABLE dbo.AspNetUsers ADD CONSTRAINT FK_AspNetUsers_USER_ACCOUNT
        FOREIGN KEY (Id) REFERENCES dbo.USER_ACCOUNT(user_id);
END;

IF OBJECT_ID(N'dbo.AspNetUserRoles', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_AspNetUserRoles_AspNetUsers_UserId')
BEGIN
    ALTER TABLE dbo.AspNetUserRoles ADD CONSTRAINT FK_AspNetUserRoles_AspNetUsers_UserId
        FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id);
    ALTER TABLE dbo.AspNetUserRoles ADD CONSTRAINT FK_AspNetUserRoles_AspNetRoles_RoleId
        FOREIGN KEY (RoleId) REFERENCES dbo.AspNetRoles(Id);
    ALTER TABLE dbo.AspNetUserClaims ADD CONSTRAINT FK_AspNetUserClaims_AspNetUsers_UserId
        FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id);
    ALTER TABLE dbo.AspNetRoleClaims ADD CONSTRAINT FK_AspNetRoleClaims_AspNetRoles_RoleId
        FOREIGN KEY (RoleId) REFERENCES dbo.AspNetRoles(Id);
    ALTER TABLE dbo.AspNetUserLogins ADD CONSTRAINT FK_AspNetUserLogins_AspNetUsers_UserId
        FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id);
    ALTER TABLE dbo.AspNetUserTokens ADD CONSTRAINT FK_AspNetUserTokens_AspNetUsers_UserId
        FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id);
END;
""";

        await db.Database.ExecuteSqlRawAsync(sql, ct);
    }
}
