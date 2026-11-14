USE [svn_pentaho];
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103010612_Printer'
)
BEGIN
    CREATE TABLE [svn_pentaho].[dbo].[Toast_Printer] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(50) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Ip] nvarchar(50) NOT NULL,
        [Port] int NOT NULL DEFAULT 9100,
        [Dpi] int NULL,
        [Location] nvarchar(100) NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Toast_Printer] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103010612_Printer'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Toast_Printer_Code] ON [svn_pentaho].[dbo].[Toast_Printer] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103010612_Printer'
)
BEGIN
    INSERT INTO [svn_pentaho].[dbo].[Toast__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261103010612_Printer', N'8.0.11');
END;
GO

COMMIT;
GO

-- =====================================================================================================
-- (Tuỳ chọn) Chép máy in từ SVN_Printer_Info sang Toast_Printer. Chạy được nhiều lần (mã đã có thì bỏ qua).
-- Bỏ dòng thiếu ID / IP; trùng ID lấy 1 dòng; Code = ID viết hoa; Name trống thì lấy ID;
-- Port không phải số hợp lệ thì lấy 9100; DPI / Vị trí để trống, nhập sau ở màn Máy in.
-- Muốn chạy: bỏ /* và */ rồi chạy riêng đoạn này.
-- =====================================================================================================
/*
INSERT INTO [svn_pentaho].[dbo].[Toast_Printer] ([Code], [Name], [Ip], [Port], [IsActive], [CreatedAt])
SELECT s.[Code], s.[Name], s.[Ip], s.[Port], 1, SYSDATETIME()
FROM (
    SELECT
        LEFT(UPPER(LTRIM(RTRIM([ID_Printer]))), 50) AS [Code],
        LEFT(COALESCE(NULLIF(LTRIM(RTRIM([Name_Printer])), N''), LTRIM(RTRIM([ID_Printer]))), 100) AS [Name],
        LEFT(LTRIM(RTRIM([IP_Printer])), 50) AS [Ip],
        CASE WHEN TRY_CAST(LTRIM(RTRIM([Port_Printer])) AS int) BETWEEN 1 AND 65535
             THEN TRY_CAST(LTRIM(RTRIM([Port_Printer])) AS int) ELSE 9100 END AS [Port],
        ROW_NUMBER() OVER (PARTITION BY UPPER(LTRIM(RTRIM([ID_Printer]))) ORDER BY (SELECT NULL)) AS [rn]
    FROM [svn_pentaho].[dbo].[SVN_Printer_Info]
    WHERE NULLIF(LTRIM(RTRIM([ID_Printer])), N'') IS NOT NULL
      AND NULLIF(LTRIM(RTRIM([IP_Printer])), N'') IS NOT NULL
) s
WHERE s.[rn] = 1
  AND NOT EXISTS (SELECT 1 FROM [svn_pentaho].[dbo].[Toast_Printer] p WHERE p.[Code] = s.[Code]);
*/
