USE [svn_pentaho];
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104082218_PrintTarget'
)
BEGIN
    ALTER TABLE [svn_pentaho].[dbo].[Toast_SessionConfirm] ADD [PrintTarget] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104082218_PrintTarget'
)
BEGIN
    ALTER TABLE [svn_pentaho].[dbo].[Toast_Package] ADD [PrintTarget] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104082218_PrintTarget'
)
BEGIN
    INSERT INTO [svn_pentaho].[dbo].[Toast__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261104082218_PrintTarget', N'8.0.11');
END;
GO

COMMIT;
GO

