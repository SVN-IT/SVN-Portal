USE [svn_pentaho];
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111023339_SessionDevice'
)
BEGIN
    ALTER TABLE [svn_pentaho].[dbo].[Toast_Session] ADD [DeviceId] nvarchar(64) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111023339_SessionDevice'
)
BEGIN
    ALTER TABLE [svn_pentaho].[dbo].[Toast_Session] ADD [LastScanSeq] bigint NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111023339_SessionDevice'
)
BEGIN
    INSERT INTO [svn_pentaho].[dbo].[Toast__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261111023339_SessionDevice', N'8.0.11');
END;
GO

COMMIT;
GO

