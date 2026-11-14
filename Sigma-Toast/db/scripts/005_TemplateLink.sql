USE [svn_pentaho];
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103033547_TemplateLink'
)
BEGIN
    DROP INDEX [IX_Toast_LabelTemplate_PartNumber_Type_Operation] ON [svn_pentaho].[dbo].[Toast_LabelTemplate];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103033547_TemplateLink'
)
BEGIN
    EXEC [svn_pentaho].[sys].[sp_rename] N'[dbo].[Toast_LabelTemplate].[PartNumber]', N'Name', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103033547_TemplateLink'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [svn_pentaho].[sys].[default_constraints] [d]
    INNER JOIN [svn_pentaho].[sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[svn_pentaho].[dbo].[Toast_LabelTemplate]') AND [c].[name] = N'Name');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [svn_pentaho].[dbo].[Toast_LabelTemplate] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [svn_pentaho].[dbo].[Toast_LabelTemplate] ALTER COLUMN [Name] nvarchar(100) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103033547_TemplateLink'
)
BEGIN
    ALTER TABLE [svn_pentaho].[dbo].[Toast_Product] ADD [BoxTemplateId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103033547_TemplateLink'
)
BEGIN
    ALTER TABLE [svn_pentaho].[dbo].[Toast_Product] ADD [OuterTemplateId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103033547_TemplateLink'
)
BEGIN
    ALTER TABLE [svn_pentaho].[dbo].[Toast_Product] ADD [Variables] nvarchar(max) NOT NULL DEFAULT N'[]';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103033547_TemplateLink'
)
BEGIN
    CREATE INDEX [IX_Toast_Product_BoxTemplateId] ON [svn_pentaho].[dbo].[Toast_Product] ([BoxTemplateId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103033547_TemplateLink'
)
BEGIN
    CREATE INDEX [IX_Toast_Product_OuterTemplateId] ON [svn_pentaho].[dbo].[Toast_Product] ([OuterTemplateId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103033547_TemplateLink'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Toast_LabelTemplate_Name_Type_Operation] ON [svn_pentaho].[dbo].[Toast_LabelTemplate] ([Name], [Type], [Operation]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103033547_TemplateLink'
)
BEGIN
    ALTER TABLE [svn_pentaho].[dbo].[Toast_Product] ADD CONSTRAINT [FK_Toast_Product_Toast_LabelTemplate_BoxTemplateId] FOREIGN KEY ([BoxTemplateId]) REFERENCES [dbo].[Toast_LabelTemplate] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103033547_TemplateLink'
)
BEGIN
    ALTER TABLE [svn_pentaho].[dbo].[Toast_Product] ADD CONSTRAINT [FK_Toast_Product_Toast_LabelTemplate_OuterTemplateId] FOREIGN KEY ([OuterTemplateId]) REFERENCES [dbo].[Toast_LabelTemplate] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103033547_TemplateLink'
)
BEGIN
    UPDATE p SET p.BoxTemplateId = (
        SELECT MIN(t.Id) FROM [svn_pentaho].[dbo].[Toast_LabelTemplate] t WHERE t.Name = p.PartNumber AND t.Type = N'Box')
    FROM [svn_pentaho].[dbo].[Toast_Product] p
    WHERE p.BoxTemplateId IS NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103033547_TemplateLink'
)
BEGIN
    INSERT INTO [svn_pentaho].[dbo].[Toast__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261103033547_TemplateLink', N'8.0.11');
END;
GO

COMMIT;
GO

