--IIS Should be installed first

--Steps to set up the GlobalRequestLogger module in IIS:

--1. Create database GlobalRequests. Run the stript init.sql
--2. Copy de file GlobalRequestLogger.Dll and open a Developer Terminal from there with high privilege
--3. Write this command
--	gacutil /i GlobalRequestLogger.dll	
--4. Open this text file 'C:\Windows\System32\inetsrv\config\applicationHost.config'
--5. Add this line inside system.webServer => modules (It's almost at the end of the document) and save the file
--	<add name="GlobalLoggerModule" type="GlobalRequestLogger.GlobalLoggerModule, GlobalRequestLogger, Version=1.0.0.0, Culture=neutral, PublicKeyToken=f934194141ceb978" preCondition="managedHandler,runtimeVersionv4.0" />
--6. Reset IIS Service. Run this command
--	iisreset

--Uninstall the module:
--1. Remove the line added in step 5 from 'C:\Windows\System32\inetsrv\config\applicationHost.config'
--2. Open a Developer Terminal with high privilege
--3. Write this command
--	gacutil /u GlobalRequestLogger
--4. Delete file located C:\Windows\Microsoft.NET\assembly\GAC_MSIL


Create database GlobalRequests
Go

Use GlobalRequests
Go

-- Create the RequestLogs table
CREATE TABLE RequestLogs (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Url NVARCHAR(MAX) NOT NULL,
    HttpMethod NVARCHAR(50) NOT NULL,
    Headers NVARCHAR(MAX) NULL,
    QueryString NVARCHAR(MAX) NULL,
    UserHostAddress NVARCHAR(50) NULL,
    UserAgent NVARCHAR(255) NULL,
    ContentType NVARCHAR(100) NULL,
    ContentLength INT NULL,
    RawUrl NVARCHAR(MAX) NULL,
    ApplicationPath NVARCHAR(255) NULL,
    CreatedAt DATETIME DEFAULT GETDATE(),
    ActionTaken NVARCHAR(50) NULL,
    ServerVariables NVARCHAR(MAX) NULL
);
GO

-- Create the stored procedure to insert data into RequestLogs
CREATE PROCEDURE InsertRequestLog
    @Url NVARCHAR(MAX),
    @HttpMethod NVARCHAR(50),
    @Headers NVARCHAR(MAX),
    @QueryString NVARCHAR(MAX),
    @UserHostAddress NVARCHAR(50),
    @UserAgent NVARCHAR(255),
    @ContentType NVARCHAR(100),
    @ContentLength INT,
    @RawUrl NVARCHAR(MAX),
    @ApplicationPath NVARCHAR(255),
    @ActionTaken NVARCHAR(50),
    @ServerVariables NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO RequestLogs (Url, HttpMethod, Headers, QueryString, UserHostAddress, UserAgent, ContentType, ContentLength, RawUrl, ApplicationPath, ActionTaken, ServerVariables)
    VALUES (@Url, @HttpMethod, @Headers, @QueryString, @UserHostAddress, @UserAgent, @ContentType, @ContentLength, @RawUrl, @ApplicationPath, @ActionTaken, @ServerVariables);
END;
GO

-- Create the ResponseLogs table
CREATE TABLE ResponseLogs (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Url NVARCHAR(MAX) NOT NULL,
    HttpMethod NVARCHAR(50) NOT NULL,
    ResponseTime BIGINT NOT NULL,
    Timestamp DATETIME NOT NULL,
    ServerVariables NVARCHAR(MAX) NULL
);
GO

-- Create the stored procedure to insert data into ResponseLogs
CREATE PROCEDURE InsertResponseLog
    @Url NVARCHAR(MAX),
    @HttpMethod NVARCHAR(50),
    @ResponseTime BIGINT,
    @Timestamp DATETIME,
    @ServerVariables NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON; -- Prevents the return of row count messages

    INSERT INTO ResponseLogs (Url, HttpMethod, ResponseTime, Timestamp, ServerVariables)
    VALUES (@Url, @HttpMethod, @ResponseTime, @Timestamp, @ServerVariables);
END;
GO

-- Step 1: Create a login for the IIS application pool identity
CREATE LOGIN [IIS APPPOOL\DefaultAppPool] FROM WINDOWS;
GO

-- Step 2: Create a user in the GlobalRequests database for the login
CREATE USER [IIS APPPOOL\DefaultAppPool] FOR LOGIN [IIS APPPOOL\DefaultAppPool];
GO
-- Step 3: Grant necessary permissions to the user
GRANT EXECUTE ON [InsertRequestLog] TO [IIS APPPOOL\DefaultAppPool];
GRANT EXECUTE ON [InsertResponseLog] TO [IIS APPPOOL\DefaultAppPool];
GRANT EXECUTE ON [GetPagedRequestLogs] TO [IIS APPPOOL\DefaultAppPool];
GRANT EXECUTE ON [GetRequestLogById] TO [IIS APPPOOL\DefaultAppPool];