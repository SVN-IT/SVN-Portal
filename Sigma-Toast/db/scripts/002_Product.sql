USE [svn_pentaho];
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103010606_Product'
)
BEGIN
    CREATE TABLE [svn_pentaho].[dbo].[Toast_Product] (
        [Id] int NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [PartNumber] nvarchar(50) NOT NULL,
        [Name] nvarchar(255) NOT NULL,
        [Barcode] nvarchar(50) NOT NULL,
        [PcsPerBox] int NOT NULL,
        [OuterType] nvarchar(20) NULL,
        [BoxPerOuter] int NULL,
        [ViindooQtyPerCode] int NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Toast_Product] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103010606_Product'
)
BEGIN
    CREATE INDEX [IX_Toast_Product_Barcode] ON [svn_pentaho].[dbo].[Toast_Product] ([Barcode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103010606_Product'
)
BEGIN
    CREATE INDEX [IX_Toast_Product_PartNumber] ON [svn_pentaho].[dbo].[Toast_Product] ([PartNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103010606_Product'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Toast_Product_ProductId] ON [svn_pentaho].[dbo].[Toast_Product] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103010606_Product'
)
BEGIN
    INSERT INTO [svn_pentaho].[dbo].[Toast__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261103010606_Product', N'8.0.11');
END;
GO

COMMIT;
GO
