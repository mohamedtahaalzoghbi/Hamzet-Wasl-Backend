using Microsoft.EntityFrameworkCore;

namespace HamzetWasl.Infrastructure.Persistence;

/// <summary>Idempotent upgrades for databases created from an older schema script. Safe to run on every start.</summary>
public static class SchemaUpgrades
{
    public static async Task ApplyAsync(HamzetWaslDbContext db, CancellationToken ct = default)
    {
        // v2.5: shop role, shop organization type, SHOP_PRODUCT. Only rewrites constraints that do not yet allow 'shop'.
        const string sql = """
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_ACCOUNT_ROLE_role_name' AND definition NOT LIKE N'%shop%')
BEGIN
    ALTER TABLE dbo.ACCOUNT_ROLE DROP CONSTRAINT CK_ACCOUNT_ROLE_role_name;
    ALTER TABLE dbo.ACCOUNT_ROLE ADD CONSTRAINT CK_ACCOUNT_ROLE_role_name CHECK (role_name IN (N'disabled_user', N'volunteer', N'association', N'company', N'shop', N'admin'));
END;
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_ORGANIZATION_organization_type' AND definition NOT LIKE N'%shop%')
BEGIN
    ALTER TABLE dbo.ORGANIZATION DROP CONSTRAINT CK_ORGANIZATION_organization_type;
    ALTER TABLE dbo.ORGANIZATION ADD CONSTRAINT CK_ORGANIZATION_organization_type CHECK (organization_type IN (N'association', N'company', N'shop'));
END;
IF OBJECT_ID(N'dbo.SHOP_PRODUCT', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SHOP_PRODUCT (
        product_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_SHOP_PRODUCT_product_id DEFAULT NEWSEQUENTIALID(),
        organization_id UNIQUEIDENTIFIER NOT NULL,
        name NVARCHAR(200) NOT NULL,
        description NVARCHAR(MAX) NULL,
        price DECIMAL(12,2) NULL,
        available BIT NOT NULL CONSTRAINT DF_SHOP_PRODUCT_available DEFAULT 1,
        created_at DATETIME2(0) NOT NULL CONSTRAINT DF_SHOP_PRODUCT_created_at DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_SHOP_PRODUCT PRIMARY KEY (product_id),
        CONSTRAINT FK_SHOP_PRODUCT_organization_id FOREIGN KEY (organization_id) REFERENCES dbo.ORGANIZATION(organization_id)
    );
    CREATE INDEX IX_SHOP_PRODUCT_organization_id ON dbo.SHOP_PRODUCT(organization_id);
END;
""";
        await db.Database.ExecuteSqlRawAsync(sql, ct);

        // v2.6: direct chat between accounts. Creates the tables only when missing.
        const string chatSql = """
IF OBJECT_ID(N'dbo.CHAT_CONVERSATION', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CHAT_CONVERSATION (
        conversation_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CHAT_CONVERSATION_conversation_id DEFAULT NEWSEQUENTIALID(),
        user_low_id UNIQUEIDENTIFIER NOT NULL,
        user_high_id UNIQUEIDENTIFIER NOT NULL,
        created_at DATETIME2(0) NOT NULL CONSTRAINT DF_CHAT_CONVERSATION_created_at DEFAULT SYSUTCDATETIME(),
        last_message_at DATETIME2(0) NULL,
        CONSTRAINT PK_CHAT_CONVERSATION PRIMARY KEY (conversation_id),
        CONSTRAINT UQ_CHAT_CONVERSATION_pair UNIQUE (user_low_id, user_high_id)
    );
END;
IF OBJECT_ID(N'dbo.CHAT_MESSAGE', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CHAT_MESSAGE (
        message_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CHAT_MESSAGE_message_id DEFAULT NEWSEQUENTIALID(),
        conversation_id UNIQUEIDENTIFIER NOT NULL,
        sender_id UNIQUEIDENTIFIER NOT NULL,
        body NVARCHAR(2000) NOT NULL,
        sent_at DATETIME2(0) NOT NULL CONSTRAINT DF_CHAT_MESSAGE_sent_at DEFAULT SYSUTCDATETIME(),
        read_at DATETIME2(0) NULL,
        CONSTRAINT PK_CHAT_MESSAGE PRIMARY KEY (message_id)
    );
END;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CHAT_CONVERSATION_user_low_id')
    ALTER TABLE dbo.CHAT_CONVERSATION ADD CONSTRAINT FK_CHAT_CONVERSATION_user_low_id FOREIGN KEY (user_low_id) REFERENCES dbo.USER_ACCOUNT(user_id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CHAT_CONVERSATION_user_high_id')
    ALTER TABLE dbo.CHAT_CONVERSATION ADD CONSTRAINT FK_CHAT_CONVERSATION_user_high_id FOREIGN KEY (user_high_id) REFERENCES dbo.USER_ACCOUNT(user_id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CHAT_MESSAGE_conversation_id')
    ALTER TABLE dbo.CHAT_MESSAGE ADD CONSTRAINT FK_CHAT_MESSAGE_conversation_id FOREIGN KEY (conversation_id) REFERENCES dbo.CHAT_CONVERSATION(conversation_id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CHAT_MESSAGE_sender_id')
    ALTER TABLE dbo.CHAT_MESSAGE ADD CONSTRAINT FK_CHAT_MESSAGE_sender_id FOREIGN KEY (sender_id) REFERENCES dbo.USER_ACCOUNT(user_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CHAT_CONVERSATION_user_low_id' AND object_id = OBJECT_ID(N'dbo.CHAT_CONVERSATION'))
    CREATE INDEX IX_CHAT_CONVERSATION_user_low_id ON dbo.CHAT_CONVERSATION(user_low_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CHAT_CONVERSATION_user_high_id' AND object_id = OBJECT_ID(N'dbo.CHAT_CONVERSATION'))
    CREATE INDEX IX_CHAT_CONVERSATION_user_high_id ON dbo.CHAT_CONVERSATION(user_high_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CHAT_MESSAGE_conversation_id' AND object_id = OBJECT_ID(N'dbo.CHAT_MESSAGE'))
    CREATE INDEX IX_CHAT_MESSAGE_conversation_id ON dbo.CHAT_MESSAGE(conversation_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CHAT_MESSAGE_sender_id' AND object_id = OBJECT_ID(N'dbo.CHAT_MESSAGE'))
    CREATE INDEX IX_CHAT_MESSAGE_sender_id ON dbo.CHAT_MESSAGE(sender_id);
""";
        await db.Database.ExecuteSqlRawAsync(chatSql, ct);

        // v2.7: role-restricted communities. Adds the column and its check only when missing.
        // The column and the constraint that references it run as separate batches: SQL Server
        // compiles a whole batch before running it, so a batch that both adds a column and
        // references it fails with "Invalid column name" on a database that predates the column.
        const string communityColumnSql = """
IF COL_LENGTH(N'dbo.COMMUNITY', N'audience_role') IS NULL
    ALTER TABLE dbo.COMMUNITY ADD audience_role NVARCHAR(50) NULL;
""";
        await db.Database.ExecuteSqlRawAsync(communityColumnSql, ct);

        const string communityConstraintSql = """
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_COMMUNITY_audience_role')
    ALTER TABLE dbo.COMMUNITY ADD CONSTRAINT CK_COMMUNITY_audience_role CHECK (audience_role IN (N'disabled_user', N'volunteer', N'association', N'company', N'shop'));
""";
        await db.Database.ExecuteSqlRawAsync(communityConstraintSql, ct);
    }
}
