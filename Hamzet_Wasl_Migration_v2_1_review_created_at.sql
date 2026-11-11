/* Hamzet Wasl - v2.1: VERIFICATION_REVIEW.created_at
   Run once on an EXISTING database that was created with Hamzet_Wasl_Schema_v2.sql.
   The fresh-install script already contains the column. Take a backup first.
   The script is idempotent: if the column exists it does nothing. */
SET XACT_ABORT ON;
BEGIN TRAN;
GO

IF COL_LENGTH('dbo.VERIFICATION_REVIEW', 'created_at') IS NULL
BEGIN
    -- 1) Add the column as nullable so existing rows can be filled first.
    ALTER TABLE dbo.VERIFICATION_REVIEW ADD created_at DATETIME2(0) NULL;

    -- 2) Existing rows have no creation timestamp. The decision (if any) is the closest real
    --    event, then the booked interview; a draft/pending row without either is dated at the
    --    migration time. All values stay in UTC (SYSUTCDATETIME), matching the API contract.
    UPDATE dbo.VERIFICATION_REVIEW
    SET created_at = COALESCE(decided_at, interview_at, SYSUTCDATETIME());

    -- 3) Enforce the same NOT NULL contract as the fresh-install script.
    ALTER TABLE dbo.VERIFICATION_REVIEW ALTER COLUMN created_at DATETIME2(0) NOT NULL;

    -- 4) Same default constraint name as the fresh-install script.
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
