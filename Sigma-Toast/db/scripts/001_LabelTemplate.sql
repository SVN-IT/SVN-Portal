USE [svn_pentaho];
GO

IF OBJECT_ID(N'[svn_pentaho].[dbo].[Toast__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [svn_pentaho].[dbo].[Toast__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK_Toast__EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030075849_LabelTemplate'
)
BEGIN
    CREATE TABLE [svn_pentaho].[dbo].[Toast_LabelTemplate] (
        [Id] int NOT NULL IDENTITY,
        [PartNumber] nvarchar(50) NOT NULL,
        [Type] nvarchar(50) NOT NULL,
        [Operation] nvarchar(50) NOT NULL,
        [Size] nvarchar(20) NOT NULL,
        [Dpi] int NOT NULL DEFAULT 600,
        [ZplData] nvarchar(max) NOT NULL,
        [Variables] nvarchar(max) NOT NULL DEFAULT N'[]',
        [IsLocked] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Toast_LabelTemplate] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030075849_LabelTemplate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Toast_LabelTemplate_PartNumber_Type_Operation] ON [svn_pentaho].[dbo].[Toast_LabelTemplate] ([PartNumber], [Type], [Operation]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030075849_LabelTemplate'
)
BEGIN
    INSERT INTO [svn_pentaho].[dbo].[Toast__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261030075849_LabelTemplate', N'8.0.11');
END;
GO

COMMIT;
GO

