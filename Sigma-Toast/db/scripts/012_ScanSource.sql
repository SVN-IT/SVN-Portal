USE [svn_pentaho];
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109022436_ScanSource'
)
BEGIN
    ALTER TABLE [svn_pentaho].[dbo].[Toast_SessionConfirm] ADD [ScanCode] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109022436_ScanSource'
)
BEGIN
    ALTER TABLE [svn_pentaho].[dbo].[Toast_Product] ADD [ScanSource] nvarchar(20) NOT NULL DEFAULT N'Barcode';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109022436_ScanSource'
)
BEGIN
    INSERT INTO [svn_pentaho].[dbo].[Toast__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261109022436_ScanSource', N'8.0.11');
END;
GO

COMMIT;
GO

