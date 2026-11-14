USE [svn_pentaho];
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103010618_Package'
)
BEGIN
    CREATE TABLE [svn_pentaho].[dbo].[Toast_Package] (
        [Id] int NOT NULL IDENTITY,
        [WoName] nvarchar(50) NOT NULL,
        [ProductId] int NOT NULL,
        [PartNumber] nvarchar(50) NOT NULL,
        [PackageNo] int NOT NULL,
        [Qty] int NOT NULL,
        [Capacity] int NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [PrinterId] int NULL,
        [PrintedAt] datetime2 NULL,
        [PrintError] nvarchar(500) NULL,
        [InputLogId] int NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ClosedAt] datetime2 NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Toast_Package] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Toast_Package_Toast_Printer_PrinterId] FOREIGN KEY ([PrinterId]) REFERENCES [dbo].[Toast_Printer] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103010618_Package'
)
BEGIN
    CREATE INDEX [IX_Toast_Package_PrinterId] ON [svn_pentaho].[dbo].[Toast_Package] ([PrinterId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103010618_Package'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Toast_Package_WoName_PackageNo] ON [svn_pentaho].[dbo].[Toast_Package] ([WoName], [PackageNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103010618_Package'
)
BEGIN
    CREATE INDEX [IX_Toast_Package_WoName_Status] ON [svn_pentaho].[dbo].[Toast_Package] ([WoName], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103010618_Package'
)
BEGIN
    INSERT INTO [svn_pentaho].[dbo].[Toast__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261103010618_Package', N'8.0.11');
END;
GO

COMMIT;
GO
