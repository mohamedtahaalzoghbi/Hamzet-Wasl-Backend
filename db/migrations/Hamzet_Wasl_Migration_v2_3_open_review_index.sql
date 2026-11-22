/* Hamzet Wasl - v2.3: one OPEN verification review per volunteer
   Run once on an EXISTING database that was created with Hamzet_Wasl_Schema_v2.sql.
   The fresh-install script already contains the index. Take a backup first.
   The script is idempotent: if the index exists it does nothing.

   The index allows at most one OPEN review round (draft, pending, interview_scheduled)
   per volunteer. Closed rounds (approved, rejected, cancelled) keep their full history,
   so repeated applications stay allowed, and a full unique constraint on volunteer_id
   would have broken that.

   The SET options are mandatory: VERIFICATION_REVIEW carries filtered indexes, and
   sqlcmd runs with QUOTED_IDENTIFIER OFF by default, which makes every UPDATE on
   that table fail with error 1934. */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
BEGIN TRAN;
GO

/* Defensive first step: if a race already created duplicate open rounds for a volunteer,
   keep the newest open round and cancel the older ones so the unique index can be
   created. Such duplicates should not exist in practice. */
WITH ranked AS (
    SELECT review_id,
           ROW_NUMBER() OVER (
               PARTITION BY volunteer_id
               ORDER BY created_at DESC, review_id DESC
           ) AS rn
    FROM dbo.VERIFICATION_REVIEW
    WHERE status IN (N'draft', N'pending', N'interview_scheduled')
)
UPDATE r
SET status = N'cancelled'
FROM dbo.VERIFICATION_REVIEW r
JOIN ranked x ON x.review_id = r.review_id
WHERE x.rn > 1;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_VERIFICATION_REVIEW_volunteer_open'
      AND object_id = OBJECT_ID(N'dbo.VERIFICATION_REVIEW')
)
BEGIN
    CREATE UNIQUE INDEX UX_VERIFICATION_REVIEW_volunteer_open
        ON dbo.VERIFICATION_REVIEW(volunteer_id)
        WHERE status IN (N'draft', N'pending', N'interview_scheduled');
END
GO

COMMIT TRAN;
GO

/* Rollback:
   DROP INDEX UX_VERIFICATION_REVIEW_volunteer_open ON dbo.VERIFICATION_REVIEW;
   (The defensive cancellation of duplicate open rounds is data, not schema, and is
   not rolled back.) */
