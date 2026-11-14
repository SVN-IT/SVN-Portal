USE [svn_pentaho];
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103071535_RevertTemplateLink'
)
BEGIN
    ALTER TABLE [svn_pentaho].[dbo].[Toast_Product] DROP CONSTRAINT [FK_Toast_Product_Toast_LabelTemplate_BoxTemplateId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103071535_RevertTemplateLink'
)
BEGIN
    ALTER TABLE [svn_pentaho].[dbo].[Toast_Product] DROP CONSTRAINT [FK_Toast_Product_Toast_LabelTemplate_OuterTemplateId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103071535_RevertTemplateLink'
)
BEGIN
    DROP INDEX [IX_Toast_Product_BoxTemplateId] ON [svn_pentaho].[dbo].[Toast_Product];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103071535_RevertTemplateLink'
)
BEGIN
    DROP INDEX [IX_Toast_Product_OuterTemplateId] ON [svn_pentaho].[dbo].[Toast_Product];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103071535_RevertTemplateLink'
)
BEGIN
    DROP INDEX [IX_Toast_LabelTemplate_Name_Type_Operation] ON [svn_pentaho].[dbo].[Toast_LabelTemplate];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103071535_RevertTemplateLink'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [svn_pentaho].[sys].[default_constraints] [d]
    INNER JOIN [svn_pentaho].[sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[Toast_Product]') AND [c].[name] = N'BoxTemplateId');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [svn_pentaho].[dbo].[Toast_Product] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [svn_pentaho].[dbo].[Toast_Product] DROP COLUMN [BoxTemplateId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103071535_RevertTemplateLink'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [svn_pentaho].[sys].[default_constraints] [d]
    INNER JOIN [svn_pentaho].[sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[Toast_Product]') AND [c].[name] = N'OuterTemplateId');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [svn_pentaho].[dbo].[Toast_Product] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [svn_pentaho].[dbo].[Toast_Product] DROP COLUMN [OuterTemplateId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103071535_RevertTemplateLink'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [svn_pentaho].[sys].[default_constraints] [d]
    INNER JOIN [svn_pentaho].[sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[Toast_Product]') AND [c].[name] = N'Variables');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [svn_pentaho].[dbo].[Toast_Product] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [svn_pentaho].[dbo].[Toast_Product] DROP COLUMN [Variables];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103071535_RevertTemplateLink'
)
BEGIN
    EXEC [svn_pentaho].[sys].[sp_rename] N'[dbo].[Toast_LabelTemplate].[Name]', N'PartNumber', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103071535_RevertTemplateLink'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [svn_pentaho].[sys].[default_constraints] [d]
    INNER JOIN [svn_pentaho].[sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[Toast_LabelTemplate]') AND [c].[name] = N'PartNumber');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [svn_pentaho].[dbo].[Toast_LabelTemplate] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [svn_pentaho].[dbo].[Toast_LabelTemplate] ALTER COLUMN [PartNumber] nvarchar(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103071535_RevertTemplateLink'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Toast_LabelTemplate_PartNumber_Type_Operation] ON [svn_pentaho].[dbo].[Toast_LabelTemplate] ([PartNumber], [Type], [Operation]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103071535_RevertTemplateLink'
)
BEGIN
    INSERT INTO [svn_pentaho].[dbo].[Toast__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261103071535_RevertTemplateLink', N'8.0.11');
END;
GO

COMMIT;
GO

