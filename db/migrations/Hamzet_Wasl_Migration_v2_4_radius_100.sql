/* Hamzet Wasl - v2.4: volunteer service radius limited to 100 km
   Run once on an EXISTING database that was created with Hamzet_Wasl_Schema_v2.sql.
   The fresh-install script already contains the tightened CHECK. Take a backup first.
   The script is idempotent: running it again changes nothing.

   The Figma location screen (design/graduation.pdf) caps the custom distance range at
   "up to 100 km", while the schema and the API allowed 1-200 km. The screens are the
   reference (design/README.md), so the bound is 100 km everywhere now
   (docs/DEVIATIONS.md #24).

   Profiles above 100 km are capped at 100 first; otherwise the new CHECK could not be
   created. Such values should not exist in practice: no client could submit them. */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
BEGIN TRAN;
GO

UPDATE dbo.VOLUNTEER_PROFILE
SET service_radius_km = 100
WHERE service_radius_km > 100;
GO

IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = N'CK_VOLUNTEER_PROFILE_radius'
      AND parent_object_id = OBJECT_ID(N'dbo.VOLUNTEER_PROFILE')
)
BEGIN
    ALTER TABLE dbo.VOLUNTEER_PROFILE DROP CONSTRAINT CK_VOLUNTEER_PROFILE_radius;
END
GO

ALTER TABLE dbo.VOLUNTEER_PROFILE WITH CHECK
    ADD CONSTRAINT CK_VOLUNTEER_PROFILE_radius
    CHECK (service_radius_km IS NULL OR service_radius_km BETWEEN 1 AND 100);
GO

COMMIT TRAN;
GO

/* Rollback:
   ALTER TABLE dbo.VOLUNTEER_PROFILE DROP CONSTRAINT CK_VOLUNTEER_PROFILE_radius;
   ALTER TABLE dbo.VOLUNTEER_PROFILE WITH CHECK
       ADD CONSTRAINT CK_VOLUNTEER_PROFILE_radius
       CHECK (service_radius_km IS NULL OR service_radius_km BETWEEN 1 AND 200);
   (The capped radius values are data, not schema, and are not rolled back.) */
