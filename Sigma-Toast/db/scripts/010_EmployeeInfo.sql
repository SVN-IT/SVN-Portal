USE [svn_pentaho];
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105021123_EmployeeInfo'
)
BEGIN
    ALTER TABLE [svn_pentaho].[dbo].[Toast_SessionConfirm] ADD [EmployeeName] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105021123_EmployeeInfo'
)
BEGIN
    ALTER TABLE [svn_pentaho].[dbo].[Toast_SessionConfirm] ADD [EmployeePhone] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [svn_pentaho].[dbo].[Toast__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105021123_EmployeeInfo'
)
BEGIN
    INSERT INTO [svn_pentaho].[dbo].[Toast__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261105021123_EmployeeInfo', N'8.0.11');
END;
GO

COMMIT;
GO

