USE [svn_pentaho];
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104073814_Session'
)
BEGIN
    ALTER TABLE [svn_pentaho].[dbo].[Toast_Package] ADD [SessionConfirmId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104073814_Session'
)
BEGIN
    CREATE TABLE [svn_pentaho].[dbo].[Toast_Session] (
        [Id] int NOT NULL IDENTITY,
        [WoName] nvarchar(50) NOT NULL,
        [ProductId] int NOT NULL,
        [DefaultCode] nvarchar(50) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [StartedAt] datetime2 NOT NULL,
        [EndedAt] datetime2 NULL,
        [EndedBy] nvarchar(50) NULL,
        CONSTRAINT [PK_Toast_Session] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104073814_Session'
)
BEGIN
    CREATE TABLE [svn_pentaho].[dbo].[Toast_SessionConfirm] (
        [Id] int NOT NULL IDENTITY,
        [SessionId] int NOT NULL,
        [EmployeeCode] nvarchar(20) NOT NULL,
        [PrinterId] int NULL,
        [ConfirmedAt] datetime2 NOT NULL,
        [Values] nvarchar(max) NOT NULL,
        [Warnings] nvarchar(max) NULL,
        CONSTRAINT [PK_Toast_SessionConfirm] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Toast_SessionConfirm_Toast_Printer_PrinterId] FOREIGN KEY ([PrinterId]) REFERENCES [dbo].[Toast_Printer] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Toast_SessionConfirm_Toast_Session_SessionId] FOREIGN KEY ([SessionId]) REFERENCES [dbo].[Toast_Session] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104073814_Session'
)
BEGIN
    CREATE INDEX [IX_Toast_Package_SessionConfirmId] ON [svn_pentaho].[dbo].[Toast_Package] ([SessionConfirmId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104073814_Session'
)
BEGIN
    CREATE INDEX [IX_Toast_Session_WoName_Status] ON [svn_pentaho].[dbo].[Toast_Session] ([WoName], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104073814_Session'
)
BEGIN
    CREATE INDEX [IX_Toast_SessionConfirm_EmployeeCode] ON [svn_pentaho].[dbo].[Toast_SessionConfirm] ([EmployeeCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104073814_Session'
)
BEGIN
    CREATE INDEX [IX_Toast_SessionConfirm_PrinterId] ON [svn_pentaho].[dbo].[Toast_SessionConfirm] ([PrinterId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104073814_Session'
)
BEGIN
    CREATE INDEX [IX_Toast_SessionConfirm_SessionId] ON [svn_pentaho].[dbo].[Toast_SessionConfirm] ([SessionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104073814_Session'
)
BEGIN
    ALTER TABLE [svn_pentaho].[dbo].[Toast_Package] ADD CONSTRAINT [FK_Toast_Package_Toast_SessionConfirm_SessionConfirmId] FOREIGN KEY ([SessionConfirmId]) REFERENCES [dbo].[Toast_SessionConfirm] ([Id]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104073814_Session'
)
BEGIN
    INSERT INTO [svn_pentaho].[dbo].[Toast__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261104073814_Session', N'8.0.11');
END;
GO

COMMIT;
GO

