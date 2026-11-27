-- v2.5: shop role, shop organization type, SHOP_PRODUCT. Idempotent; the API also applies this at startup.
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_ACCOUNT_ROLE_role_name')
    ALTER TABLE dbo.ACCOUNT_ROLE DROP CONSTRAINT CK_ACCOUNT_ROLE_role_name;
ALTER TABLE dbo.ACCOUNT_ROLE ADD CONSTRAINT CK_ACCOUNT_ROLE_role_name CHECK (role_name IN (N'disabled_user', N'volunteer', N'association', N'company', N'shop', N'admin'));
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_ORGANIZATION_organization_type')
    ALTER TABLE dbo.ORGANIZATION DROP CONSTRAINT CK_ORGANIZATION_organization_type;
ALTER TABLE dbo.ORGANIZATION ADD CONSTRAINT CK_ORGANIZATION_organization_type CHECK (organization_type IN (N'association', N'company', N'shop'));
IF NOT EXISTS (SELECT 1 FROM dbo.ACCOUNT_ROLE WHERE role_name = N'shop') INSERT INTO dbo.ACCOUNT_ROLE (role_name) VALUES (N'shop');
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
