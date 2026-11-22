/* Migrate an EXISTING v1 database to v2. Run once, inside a transaction, on a backup first. */
SET XACT_ABORT ON;
BEGIN TRAN;
GO
-- 1) phone
ALTER TABLE dbo.USER_ACCOUNT ADD phone NVARCHAR(30) NULL;
ALTER TABLE dbo.USER_ACCOUNT ADD phone_verified BIT NOT NULL CONSTRAINT DF_USER_ACCOUNT_phone_verified DEFAULT 0;
GO
CREATE UNIQUE INDEX UX_USER_ACCOUNT_phone ON dbo.USER_ACCOUNT(phone) WHERE phone IS NOT NULL;
CREATE TABLE dbo.PHONE_VERIFICATION (
    verification_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_PHONE_VERIFICATION_verification_id DEFAULT NEWSEQUENTIALID(),
    user_id UNIQUEIDENTIFIER NOT NULL,
    code_hash NVARCHAR(200) NOT NULL,
    expires_at DATETIME2(0) NOT NULL,
    verified_at DATETIME2(0) NULL,
    CONSTRAINT PK_PHONE_VERIFICATION PRIMARY KEY (verification_id),
    CONSTRAINT FK_PHONE_VERIFICATION_user_id FOREIGN KEY (user_id) REFERENCES dbo.USER_ACCOUNT(user_id)
);
CREATE INDEX IX_PHONE_VERIFICATION_user_id ON dbo.PHONE_VERIFICATION(user_id);
GO
-- 2) one need type per user: USER_NEED -> ACCESSIBILITY_PROFILE
ALTER TABLE dbo.ACCESSIBILITY_PROFILE ADD need_type_id INT NULL, consent_id UNIQUEIDENTIFIER NULL;
GO
-- keep the most recently declared need when a user had several (REVIEW this choice before running)
UPDATE ap SET ap.need_type_id = un.need_type_id, ap.consent_id = un.consent_id
FROM dbo.ACCESSIBILITY_PROFILE ap
CROSS APPLY (SELECT TOP 1 need_type_id, consent_id FROM dbo.USER_NEED n WHERE n.user_id = ap.user_id ORDER BY declared_at DESC) un;
-- profiles without any USER_NEED cannot be migrated automatically: fix or delete them before the next statements
ALTER TABLE dbo.ACCESSIBILITY_PROFILE ALTER COLUMN need_type_id INT NOT NULL;
ALTER TABLE dbo.ACCESSIBILITY_PROFILE ALTER COLUMN consent_id UNIQUEIDENTIFIER NOT NULL;
ALTER TABLE dbo.ACCESSIBILITY_PROFILE ADD CONSTRAINT FK_ACCESSIBILITY_PROFILE_need_type_id FOREIGN KEY (need_type_id) REFERENCES dbo.NEED_TYPE(need_type_id);
ALTER TABLE dbo.ACCESSIBILITY_PROFILE ADD CONSTRAINT FK_ACCESSIBILITY_PROFILE_consent_id FOREIGN KEY (consent_id) REFERENCES dbo.CONSENT_RECORD(consent_id);
CREATE INDEX IX_ACCESSIBILITY_PROFILE_need_type_id ON dbo.ACCESSIBILITY_PROFILE(need_type_id);
CREATE INDEX IX_ACCESSIBILITY_PROFILE_consent_id ON dbo.ACCESSIBILITY_PROFILE(consent_id);
DROP TABLE dbo.USER_NEED;
GO
-- 3) USER_SKILL
CREATE TABLE dbo.USER_SKILL (
    user_id UNIQUEIDENTIFIER NOT NULL,
    skill_id INT NOT NULL,
    CONSTRAINT PK_USER_SKILL PRIMARY KEY (user_id, skill_id),
    CONSTRAINT FK_USER_SKILL_user_id FOREIGN KEY (user_id) REFERENCES dbo.USER_ACCOUNT(user_id),
    CONSTRAINT FK_USER_SKILL_skill_id FOREIGN KEY (skill_id) REFERENCES dbo.SKILL(skill_id)
);
CREATE INDEX IX_USER_SKILL_skill_id ON dbo.USER_SKILL(skill_id);
GO
-- 4) volunteer location / radius
ALTER TABLE dbo.VOLUNTEER_PROFILE ADD governorate NVARCHAR(100) NULL, latitude DECIMAL(9,6) NULL, longitude DECIMAL(9,6) NULL, service_radius_km INT NULL;
GO
ALTER TABLE dbo.VOLUNTEER_PROFILE ADD CONSTRAINT CK_VOLUNTEER_PROFILE_radius CHECK (service_radius_km IS NULL OR service_radius_km BETWEEN 1 AND 200);
GO
-- 5) organization documents / social links
ALTER TABLE dbo.ORGANIZATION ADD employee_count_range NVARCHAR(50) NULL, cover_storage_key NVARCHAR(500) NULL, governorate NVARCHAR(100) NULL;
GO
CREATE TABLE dbo.ORGANIZATION_DOCUMENT (
    document_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ORGANIZATION_DOCUMENT_document_id DEFAULT NEWSEQUENTIALID(),
    organization_id UNIQUEIDENTIFIER NOT NULL,
    document_type NVARCHAR(50) NOT NULL,
    storage_key NVARCHAR(500) NOT NULL,
    uploaded_at DATETIME2(0) NOT NULL CONSTRAINT DF_ORGANIZATION_DOCUMENT_uploaded_at DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_ORGANIZATION_DOCUMENT PRIMARY KEY (document_id),
    CONSTRAINT UQ_ORGANIZATION_DOCUMENT_type UNIQUE (organization_id, document_type),
    CONSTRAINT CK_ORGANIZATION_DOCUMENT_type CHECK (document_type IN (N'commercial_register', N'tax_card', N'address_proof', N'license')),
    CONSTRAINT FK_ORGANIZATION_DOCUMENT_organization_id FOREIGN KEY (organization_id) REFERENCES dbo.ORGANIZATION(organization_id)
);
CREATE INDEX IX_ORGANIZATION_DOCUMENT_organization_id ON dbo.ORGANIZATION_DOCUMENT(organization_id);
GO
-- 6) volunteer social links + interview slots
CREATE TABLE dbo.VOLUNTEER_SOCIAL_LINK (
    volunteer_id UNIQUEIDENTIFIER NOT NULL,
    platform NVARCHAR(50) NOT NULL,
    url NVARCHAR(500) NOT NULL,
    CONSTRAINT PK_VOLUNTEER_SOCIAL_LINK PRIMARY KEY (volunteer_id, platform),
    CONSTRAINT CK_VOLUNTEER_SOCIAL_LINK_platform CHECK (platform IN (N'facebook', N'linkedin', N'instagram')),
    CONSTRAINT FK_VOLUNTEER_SOCIAL_LINK_volunteer_id FOREIGN KEY (volunteer_id) REFERENCES dbo.VOLUNTEER_PROFILE(user_id)
);
CREATE TABLE dbo.INTERVIEW_SLOT (
    interview_slot_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_INTERVIEW_SLOT_interview_slot_id DEFAULT NEWSEQUENTIALID(),
    reviewer_id UNIQUEIDENTIFIER NOT NULL,
    starts_at DATETIME2(0) NOT NULL,
    duration_minutes INT NOT NULL CONSTRAINT DF_INTERVIEW_SLOT_duration DEFAULT 30,
    status NVARCHAR(50) NOT NULL CONSTRAINT DF_INTERVIEW_SLOT_status DEFAULT N'available',
    CONSTRAINT PK_INTERVIEW_SLOT PRIMARY KEY (interview_slot_id),
    CONSTRAINT UQ_INTERVIEW_SLOT_reviewer_start UNIQUE (reviewer_id, starts_at),
    CONSTRAINT CK_INTERVIEW_SLOT_status CHECK (status IN (N'available', N'booked', N'unavailable')),
    CONSTRAINT FK_INTERVIEW_SLOT_reviewer_id FOREIGN KEY (reviewer_id) REFERENCES dbo.USER_ACCOUNT(user_id)
);
CREATE INDEX IX_INTERVIEW_SLOT_reviewer_id ON dbo.INTERVIEW_SLOT(reviewer_id);
ALTER TABLE dbo.VERIFICATION_REVIEW ADD interview_slot_id UNIQUEIDENTIFIER NULL;
GO
ALTER TABLE dbo.VERIFICATION_REVIEW ADD CONSTRAINT FK_VERIFICATION_REVIEW_interview_slot_id FOREIGN KEY (interview_slot_id) REFERENCES dbo.INTERVIEW_SLOT(interview_slot_id);
CREATE UNIQUE INDEX UX_VERIFICATION_REVIEW_interview_slot ON dbo.VERIFICATION_REVIEW(interview_slot_id) WHERE interview_slot_id IS NOT NULL;
GO
-- keep existing license files, then drop the old column
INSERT INTO dbo.ORGANIZATION_DOCUMENT (organization_id, document_type, storage_key)
SELECT organization_id, N'license', license_storage_key FROM dbo.ORGANIZATION WHERE license_storage_key IS NOT NULL;
ALTER TABLE dbo.ORGANIZATION DROP COLUMN license_storage_key;
GO
COMMIT TRAN;
