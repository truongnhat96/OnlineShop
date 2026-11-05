SET NOCOUNT ON;
IF DB_NAME() <> N'OnlineShop' THROW 50000, 'Unexpected database', 1;
DECLARE @directory nvarchar(4000) = CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(4000));
IF @directory IS NULL THROW 50001, 'SQL Server backup directory unavailable', 1;
IF RIGHT(@directory, 1) <> N'\' SET @directory += N'\';
DECLARE @file nvarchar(4000) = @directory + N'OnlineShop-before-recommendations-' + CONVERT(nvarchar(36), NEWID()) + N'.bak';
BACKUP DATABASE [OnlineShop] TO DISK = @file WITH COPY_ONLY, CHECKSUM;
RESTORE VERIFYONLY FROM DISK = @file WITH CHECKSUM;
SELECT @file AS BackupFile;
