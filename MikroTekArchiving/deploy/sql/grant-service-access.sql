-- Lets the Windows Service (runs as NT AUTHORITY\SYSTEM) use the database.
-- Only needed when you use install-service.bat. Safe to run more than once.
USE master;
IF SUSER_ID(N'NT AUTHORITY\SYSTEM') IS NULL
    CREATE LOGIN [NT AUTHORITY\SYSTEM] FROM WINDOWS;
ALTER SERVER ROLE dbcreator ADD MEMBER [NT AUTHORITY\SYSTEM];
GO
IF DB_ID(N'MikroTekArchivingDb') IS NOT NULL
BEGIN
    EXEC(N'USE [MikroTekArchivingDb];
      IF USER_ID(N''NT AUTHORITY\SYSTEM'') IS NULL
          CREATE USER [NT AUTHORITY\SYSTEM] FOR LOGIN [NT AUTHORITY\SYSTEM];
      ALTER ROLE db_owner ADD MEMBER [NT AUTHORITY\SYSTEM];');
END
GO
