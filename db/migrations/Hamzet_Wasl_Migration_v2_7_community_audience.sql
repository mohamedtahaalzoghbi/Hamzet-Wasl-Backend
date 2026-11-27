-- v2.7: role-restricted communities (COMMUNITY.audience_role). Idempotent; the API also applies this at startup.
-- NULL means open to every role; a set value (e.g. 'disabled_user') hides the community
-- and its posts from every other role (admin still sees everything for moderation).
IF COL_LENGTH(N'dbo.COMMUNITY', N'audience_role') IS NULL
    ALTER TABLE dbo.COMMUNITY ADD audience_role NVARCHAR(50) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_COMMUNITY_audience_role')
    ALTER TABLE dbo.COMMUNITY ADD CONSTRAINT CK_COMMUNITY_audience_role CHECK (audience_role IN (N'disabled_user', N'volunteer', N'association', N'company', N'shop'));
