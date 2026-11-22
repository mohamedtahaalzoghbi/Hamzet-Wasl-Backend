/* Hamzet Wasl - v2.2: dynamic governorate / area catalogue (GOVERNORATE + AREA).
   Run once on an EXISTING database created with Hamzet_Wasl_Schema_v2.sql.
   The fresh-install script already contains both tables. Take a backup first.
   The script is idempotent: it creates missing tables and seeds them only when empty.

   The SET options are mandatory: some tables carry filtered indexes, and sqlcmd runs
   with QUOTED_IDENTIFIER OFF by default, which makes the writes fail with error 1934. */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
BEGIN TRAN;
GO

IF OBJECT_ID('dbo.GOVERNORATE', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.GOVERNORATE (
        governorate_id INT IDENTITY(1,1) NOT NULL,
        code NVARCHAR(50) NOT NULL,
        name_ar NVARCHAR(100) NOT NULL,
        is_active BIT NOT NULL CONSTRAINT DF_GOVERNORATE_is_active DEFAULT 1,
        CONSTRAINT PK_GOVERNORATE PRIMARY KEY (governorate_id),
        CONSTRAINT UQ_GOVERNORATE_code UNIQUE (code)
    );

    CREATE TABLE dbo.AREA (
        area_id INT IDENTITY(1,1) NOT NULL,
        governorate_id INT NOT NULL,
        code NVARCHAR(50) NOT NULL,
        name_ar NVARCHAR(100) NOT NULL,
        is_active BIT NOT NULL CONSTRAINT DF_AREA_is_active DEFAULT 1,
        CONSTRAINT PK_AREA PRIMARY KEY (area_id),
        CONSTRAINT UQ_AREA_governorate_code UNIQUE (governorate_id, code)
    );

    ALTER TABLE dbo.AREA
        ADD CONSTRAINT FK_AREA_governorate_id FOREIGN KEY (governorate_id)
        REFERENCES dbo.GOVERNORATE(governorate_id);

    CREATE INDEX IX_AREA_governorate_id ON dbo.AREA(governorate_id);
END
GO

/* Initial data mirrors MetaCatalog.Governorates. Existing databases may already have rows:
   seed only when the table is empty, so an admin's edits are never overwritten. */
IF NOT EXISTS (SELECT 1 FROM dbo.GOVERNORATE)
BEGIN
    INSERT INTO dbo.GOVERNORATE (code, name_ar) VALUES
        (N'cairo', N'القاهرة'),
        (N'giza', N'الجيزة'),
        (N'alexandria', N'الإسكندرية'),
        (N'dakahlia', N'الدقهلية'),
        (N'sharqia', N'الشرقية'),
        (N'gharbia', N'الغربية'),
        (N'monufia', N'المنوفية'),
        (N'qalyubia', N'القليوبية'),
        (N'kafr_el_sheikh', N'كفر الشيخ'),
        (N'damietta', N'دمياط'),
        (N'port_said', N'بورسعيد'),
        (N'ismailia', N'الإسماعيلية'),
        (N'suez', N'السويس'),
        (N'north_sinai', N'شمال سيناء'),
        (N'south_sinai', N'جنوب سيناء'),
        (N'red_sea', N'البحر الأحمر'),
        (N'beheira', N'البحيرة'),
        (N'matrouh', N'مطروح'),
        (N'fayoum', N'الفيوم'),
        (N'beni_suef', N'بني سويف'),
        (N'minya', N'المنيا'),
        (N'assiut', N'أسيوط'),
        (N'sohag', N'سوهاج'),
        (N'qena', N'قنا'),
        (N'luxor', N'الأقصر'),
        (N'aswan', N'أسوان'),
        (N'new_valley', N'الوادي الجديد');

    INSERT INTO dbo.AREA (governorate_id, code, name_ar)
    SELECT g.governorate_id, v.code, v.name_ar
    FROM (VALUES
        (N'cairo', N'nasr_city', N'مدينة نصر'),
        (N'cairo', N'maadi', N'المعادي'),
        (N'cairo', N'heliopolis', N'مصر الجديدة'),
        (N'cairo', N'downtown', N'وسط البلد'),
        (N'cairo', N'zamalek', N'الزمالك'),
        (N'giza', N'dokki', N'الدقي'),
        (N'giza', N'sheikh_zayed', N'الشيخ زايد'),
        (N'giza', N'haram', N'الهرم'),
        (N'giza', N'october_6', N'6 أكتوبر'),
        (N'alexandria', N'smouha', N'سموحة'),
        (N'alexandria', N'sidi_gaber', N'سيدي جابر'),
        (N'alexandria', N'montazah', N'المنتزه'),
        (N'dakahlia', N'mansoura', N'المنصورة'),
        (N'dakahlia', N'mit_ghamr', N'ميت غمر'),
        (N'sharqia', N'zagazig', N'الزقازيق'),
        (N'sharqia', N'10th_of_ramadan', N'العاشر من رمضان'),
        (N'gharbia', N'tanta', N'طنطا'),
        (N'gharbia', N'mahalla', N'المحلة الكبرى'),
        (N'monufia', N'shebin_el_kom', N'شبين الكوم'),
        (N'qalyubia', N'banha', N'بنها'),
        (N'qalyubia', N'shubra_el_kheima', N'شبرا الخيمة'),
        (N'kafr_el_sheikh', N'kafr_el_sheikh_city', N'كفر الشيخ'),
        (N'damietta', N'damietta_city', N'دمياط'),
        (N'port_said', N'port_said_city', N'بورسعيد'),
        (N'ismailia', N'ismailia_city', N'الإسماعيلية'),
        (N'suez', N'suez_city', N'السويس'),
        (N'north_sinai', N'arish', N'العريش'),
        (N'south_sinai', N'sharm_el_sheikh', N'شرم الشيخ'),
        (N'red_sea', N'hurghada', N'الغردقة'),
        (N'beheira', N'damanhur', N'دمنهور'),
        (N'matrouh', N'marsa_matrouh', N'مرسى مطروح'),
        (N'fayoum', N'fayoum_city', N'الفيوم'),
        (N'beni_suef', N'beni_suef_city', N'بني سويف'),
        (N'minya', N'minya_city', N'المنيا'),
        (N'assiut', N'assiut_city', N'أسيوط'),
        (N'assiut', N'abnoub', N'أبنوب'),
        (N'sohag', N'sohag_city', N'سوهاج'),
        (N'qena', N'qena_city', N'قنا'),
        (N'luxor', N'luxor_city', N'الأقصر'),
        (N'aswan', N'aswan_city', N'أسوان'),
        (N'new_valley', N'kharga', N'الخارجة')
    ) AS v(gov_code, code, name_ar)
    INNER JOIN dbo.GOVERNORATE AS g ON g.code = v.gov_code;
END
GO

COMMIT TRAN;
GO

/* Rollback (run manually only if needed):
   DROP INDEX IX_AREA_governorate_id ON dbo.AREA;
   ALTER TABLE dbo.AREA DROP CONSTRAINT FK_AREA_governorate_id;
   DROP TABLE dbo.AREA;
   DROP TABLE dbo.GOVERNORATE;
*/
