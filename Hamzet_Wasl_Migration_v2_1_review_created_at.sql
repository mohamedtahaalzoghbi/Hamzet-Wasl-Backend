/* Hamzet Wasl - v2.1: VERIFICATION_REVIEW.created_at
   Run once on an EXISTING database that was created with Hamzet_Wasl_Schema_v2.sql.
   The fresh-install script already contains the column. Take a backup first.
   The script is idempotent: if the column exists it does nothing.

   The SET options are mandatory: VERIFICATION_REVIEW carries a filtered index, and
   sqlcmd runs with QUOTED_IDENTIFIER OFF by default, which makes every UPDATE on
   that table fail with error 1934. */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
BEGIN TRAN;
GO

/* The statements below are split into separate batches on purpose: T-SQL compiles a whole
   batch before it runs it, so a statement that mentions the new column cannot live in
   the same batch as the ALTER TABLE that creates it. */
IF COL_LENGTH('dbo.VERIFICATION_REVIEW', 'created_at') IS NULL
BEGIN
    -- 1) Add the column as nullable so existing rows can be filled first.
    ALTER TABLE dbo.VERIFICATION_REVIEW ADD created_at DATETIME2(0) NULL;
END
GO

-- 2) Existing rows have no creation timestamp. The decision (if any) is the closest real
--    event, then the booked interview; a draft/pending row without either is dated at the
--    migration time. All values stay in UTC (SYSUTCDATETIME), matching the API contract.
IF EXISTS (SELECT 1 FROM dbo.VERIFICATION_REVIEW WHERE created_at IS NULL)
BEGIN
    UPDATE dbo.VERIFICATION_REVIEW
    SET created_at = COALESCE(decided_at, interview_at, SYSUTCDATETIME())
    WHERE created_at IS NULL;
END
GO

-- 3) Enforce the same NOT NULL contract as the fresh-install script and give the column
--    the same default constraint name.
IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID('dbo.VERIFICATION_REVIEW')
             AND name = 'created_at' AND is_nullable = 1)
BEGIN
    ALTER TABLE dbo.VERIFICATION_REVIEW ALTER COLUMN created_at DATETIME2(0) NOT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_VERIFICATION_REVIEW_created_at')
BEGIN
    ALTER TABLE dbo.VERIFICATION_REVIEW
        ADD CONSTRAINT DF_VERIFICATION_REVIEW_created_at DEFAULT SYSUTCDATETIME() FOR created_at;
END
GO

COMMIT TRAN;
GO

/* Rollback (run manually only if needed):
   ALTER TABLE dbo.VERIFICATION_REVIEW DROP CONSTRAINT DF_VERIFICATION_REVIEW_created_at;
   ALTER TABLE dbo.VERIFICATION_REVIEW DROP COLUMN created_at;
*/
