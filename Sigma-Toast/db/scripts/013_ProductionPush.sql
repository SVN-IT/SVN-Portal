USE [svn_pentaho];
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109063124_ProductionPush'
)
BEGIN
    ALTER TABLE [svn_pentaho].[dbo].[Toast_Session] ADD [PushDetail] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109063124_ProductionPush'
)
BEGIN
    ALTER TABLE [svn_pentaho].[dbo].[Toast_Session] ADD [PushMessage] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109063124_ProductionPush'
)
BEGIN
    ALTER TABLE [svn_pentaho].[dbo].[Toast_Session] ADD [PushQty] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109063124_ProductionPush'
)
BEGIN
    ALTER TABLE [svn_pentaho].[dbo].[Toast_Session] ADD [PushStatus] nvarchar(20) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109063124_ProductionPush'
)
BEGIN
    ALTER TABLE [svn_pentaho].[dbo].[Toast_Session] ADD [PushedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109063124_ProductionPush'
)
BEGIN
    INSERT INTO [svn_pentaho].[dbo].[Toast__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261109063124_ProductionPush', N'8.0.11');
END;
GO

COMMIT;
GO

