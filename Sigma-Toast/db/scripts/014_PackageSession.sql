USE [svn_pentaho];
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109081842_PackageSession'
)
BEGIN
    ALTER TABLE [svn_pentaho].[dbo].[Toast_Package] DROP CONSTRAINT [FK_Toast_Package_Toast_SessionConfirm_SessionConfirmId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109081842_PackageSession'
)
BEGIN
    EXEC [svn_pentaho].[sys].[sp_rename] N'[dbo].[Toast_Package].[SessionConfirmId]', N'SessionId', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109081842_PackageSession'
)
BEGIN
    EXEC [svn_pentaho].[sys].[sp_rename] N'[dbo].[Toast_Package].[IX_Toast_Package_SessionConfirmId]', N'IX_Toast_Package_SessionId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109081842_PackageSession'
)
BEGIN
    UPDATE p SET p.[SessionId] = c.[SessionId]
    FROM [svn_pentaho].[dbo].[Toast_Package] p
    JOIN [svn_pentaho].[dbo].[Toast_SessionConfirm] c ON c.[Id] = p.[SessionId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109081842_PackageSession'
)
BEGIN
    ALTER TABLE [svn_pentaho].[dbo].[Toast_Package] ADD CONSTRAINT [FK_Toast_Package_Toast_Session_SessionId] FOREIGN KEY ([SessionId]) REFERENCES [dbo].[Toast_Session] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109081842_PackageSession'
)
BEGIN
    INSERT INTO [svn_pentaho].[dbo].[Toast__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261109081842_PackageSession', N'8.0.11');
END;
GO

COMMIT;
GO

