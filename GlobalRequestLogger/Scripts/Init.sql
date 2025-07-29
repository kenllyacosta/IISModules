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
    CreatedAt DATETIME DEFAULT GETDATE()
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
    @ApplicationPath NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO RequestLogs (Url, HttpMethod, Headers, QueryString, UserHostAddress, UserAgent, ContentType, ContentLength, RawUrl, ApplicationPath)
    VALUES (@Url, @HttpMethod, @Headers, @QueryString, @UserHostAddress, @UserAgent, @ContentType, @ContentLength, @RawUrl, @ApplicationPath);
END;

-- Create the ResponseLogs table
CREATE TABLE ResponseLogs (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Url NVARCHAR(MAX) NOT NULL,
    HttpMethod NVARCHAR(50) NOT NULL,
    ResponseTime BIGINT NOT NULL,
    Timestamp DATETIME NOT NULL
);
GO
-- Create the stored procedure to insert data into ResponseLogs
CREATE PROCEDURE InsertResponseLog
    @Url NVARCHAR(MAX),
    @HttpMethod NVARCHAR(50),
    @ResponseTime BIGINT,
    @Timestamp DATETIME
AS
BEGIN
    SET NOCOUNT ON; -- Prevents the return of row count messages

    INSERT INTO ResponseLogs (Url, HttpMethod, ResponseTime, Timestamp)
    VALUES (@Url, @HttpMethod, @ResponseTime, @Timestamp);
END;

GO

-- Procedure to retrieve the last 10 records in a paged manner
CREATE PROCEDURE GetPagedRequestLogs
    @PageNumber INT,
    @PageSize INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM (
        SELECT 
            ROW_NUMBER() OVER (ORDER BY CreatedAt DESC) AS RowNum,
            Id,
            Url,
            HttpMethod,
            Headers,
            QueryString,
            UserHostAddress,
            UserAgent,
            ContentType,
            ContentLength,
            RawUrl,
            ApplicationPath,
            CreatedAt
        FROM RequestLogs
    ) AS Paged
    WHERE RowNum BETWEEN (@PageNumber - 1) * @PageSize + 1 AND @PageNumber * @PageSize
    ORDER BY RowNum;
END;
GO

-- Procedure to retrieve a record by its ID
CREATE PROCEDURE GetRequestLogById
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        Id,
        Url,
        HttpMethod,
        Headers,
        QueryString,
        UserHostAddress,
        UserAgent,
        ContentType,
        ContentLength,
        RawUrl,
        ApplicationPath,
        CreatedAt
    FROM RequestLogs
    WHERE Id = @Id;
END;
GO

-- Step 1: Create a login for the IIS application pool identity
CREATE LOGIN [IIS APPPOOL\DefaultAppPool] FROM WINDOWS;

-- Step 2: Create a user in the GlobalRequests database for the login
USE GlobalRequests;
CREATE USER [IIS APPPOOL\DefaultAppPool] FOR LOGIN [IIS APPPOOL\DefaultAppPool];

-- Step 3: Grant necessary permissions to the user
-- Grant execute permissions on the stored procedures
GRANT EXECUTE ON [InsertRequestLog] TO [IIS APPPOOL\DefaultAppPool];
GRANT EXECUTE ON [InsertResponseLog] TO [IIS APPPOOL\DefaultAppPool];
GRANT EXECUTE ON [GetPagedRequestLogs] TO [IIS APPPOOL\DefaultAppPool];
GRANT EXECUTE ON [GetRequestLogById] TO [IIS APPPOOL\DefaultAppPool];