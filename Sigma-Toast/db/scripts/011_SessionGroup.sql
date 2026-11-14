USE [svn_pentaho];
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105064239_SessionGroup'
)
BEGIN
    ALTER TABLE [svn_pentaho].[dbo].[Toast_Session] ADD [ViindooMoId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105064239_SessionGroup'
)
BEGIN
    ALTER TABLE [svn_pentaho].[dbo].[Toast_Session] ADD [WoGroup] nvarchar(50) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105064239_SessionGroup'
)
BEGIN
    ALTER TABLE [svn_pentaho].[dbo].[Toast_Package] ADD [WoGroup] nvarchar(50) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105064239_SessionGroup'
)
BEGIN
    UPDATE [svn_pentaho].[dbo].[Toast_Session] SET [WoGroup] = CASE WHEN CHARINDEX('-', [WoName]) > 0 THEN LEFT([WoName], CHARINDEX('-', [WoName]) - 1) ELSE [WoName] END WHERE [WoGroup] = N''
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105064239_SessionGroup'
)
BEGIN
    UPDATE [svn_pentaho].[dbo].[Toast_Package] SET [WoGroup] = CASE WHEN CHARINDEX('-', [WoName]) > 0 THEN LEFT([WoName], CHARINDEX('-', [WoName]) - 1) ELSE [WoName] END WHERE [WoGroup] = N''
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105064239_SessionGroup'
)
BEGIN
    CREATE INDEX [IX_Toast_Session_WoGroup_Status] ON [svn_pentaho].[dbo].[Toast_Session] ([WoGroup], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105064239_SessionGroup'
)
BEGIN
    CREATE INDEX [IX_Toast_Package_WoGroup_Status] ON [svn_pentaho].[dbo].[Toast_Package] ([WoGroup], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105064239_SessionGroup'
)
BEGIN
    INSERT INTO [svn_pentaho].[dbo].[Toast__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261105064239_SessionGroup', N'8.0.11');
END;
GO

COMMIT;
GO

