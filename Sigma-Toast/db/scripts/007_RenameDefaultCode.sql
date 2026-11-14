USE [svn_pentaho];
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103072221_RenameDefaultCode'
)
BEGIN
    EXEC [svn_pentaho].[sys].[sp_rename] N'[dbo].[Toast_Product].[PartNumber]', N'DefaultCode', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103072221_RenameDefaultCode'
)
BEGIN
    EXEC [svn_pentaho].[sys].[sp_rename] N'[dbo].[Toast_Product].[IX_Toast_Product_PartNumber]', N'IX_Toast_Product_DefaultCode', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103072221_RenameDefaultCode'
)
BEGIN
    EXEC [svn_pentaho].[sys].[sp_rename] N'[dbo].[Toast_Package].[PartNumber]', N'DefaultCode', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103072221_RenameDefaultCode'
)
BEGIN
    EXEC [svn_pentaho].[sys].[sp_rename] N'[dbo].[Toast_LabelTemplate].[PartNumber]', N'DefaultCode', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103072221_RenameDefaultCode'
)
BEGIN
    EXEC [svn_pentaho].[sys].[sp_rename] N'[dbo].[Toast_LabelTemplate].[IX_Toast_LabelTemplate_PartNumber_Type_Operation]', N'IX_Toast_LabelTemplate_DefaultCode_Type_Operation', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103072221_RenameDefaultCode'
)
BEGIN
    INSERT INTO [svn_pentaho].[dbo].[Toast__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261103072221_RenameDefaultCode', N'8.0.11');
END;
GO

COMMIT;
GO

