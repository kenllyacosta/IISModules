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

USE [master]
Go
CREATE DATABASE [GlobalRequests]
GO
USE [GlobalRequests]
GO

CREATE TABLE [dbo].[AppEntity](
	[Id] [uniqueidentifier] NOT NULL,
	[AppName] [nvarchar](255) NOT NULL,
	[AppDescription] [nvarchar](max) NULL,
	[Host] [nvarchar](128) NOT NULL,
	[CreationDate] [datetime] NULL,
	[TokenExpirationDurationHr] [tinyint] NULL,
PRIMARY KEY CLUSTERED 
([Id] ASC) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_AppEntity_Host] UNIQUE NONCLUSTERED ([Host] ASC) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

CREATE TABLE [dbo].[RequestLogs](
	[Id] [uniqueidentifier] NOT NULL,
	[Url] [nvarchar](max) NOT NULL,
	[HttpMethod] [nvarchar](50) NOT NULL,
	[Headers] [nvarchar](max) NULL,
	[QueryString] [nvarchar](max) NULL,
	[UserHostAddress] [nvarchar](50) NULL,
	[UserAgent] [nvarchar](255) NULL,
	[ContentType] [nvarchar](100) NULL,
	[ContentLength] [int] NULL,
	[RawUrl] [nvarchar](max) NULL,
	[ApplicationPath] [nvarchar](255) NULL,
	[CreatedAt] [datetime] NULL,
	[ActionTaken] [nvarchar](50) NULL,
	[ServerVariables] [nvarchar](max) NULL,
	[IdRuleTriggered] [int] NULL,
    [Host] [varchar](200) NULL
PRIMARY KEY CLUSTERED 
([Id] ASC) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

CREATE TABLE [dbo].[ResponseLogs](
	[Id] [uniqueidentifier] NOT NULL,
	[Url] [nvarchar](max) NOT NULL,
	[HttpMethod] [nvarchar](50) NOT NULL,
	[ResponseTime] [bigint] NOT NULL,
	[Timestamp] [datetime] NOT NULL,
	[ServerVariables] [nvarchar](max) NULL,
PRIMARY KEY CLUSTERED 
([Id] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

CREATE TABLE [dbo].[WafConditionEntity](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Campo] [nvarchar](100) NULL,
	[Operador] [nvarchar](50) NULL,
	[Valor] [nvarchar](1000) NULL,
	[Logica] [nvarchar](10) NULL,
	[WafRuleEntityId] [int] NOT NULL,
	[CreationDate] [datetime] NULL,
PRIMARY KEY CLUSTERED 
([Id] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]) ON [PRIMARY]
GO

CREATE TABLE [dbo].[WafRuleEntity](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Nombre] [nvarchar](255) NULL,
	[Accion] [nvarchar](50) NULL,
	[AppId] [uniqueidentifier] NOT NULL,
	[Prioridad] [int] NULL,
	[Habilitado] [bit] NULL,
	[CreationDate] [datetime] NULL,
PRIMARY KEY CLUSTERED 
([Id] ASC)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]) ON [PRIMARY]
GO

ALTER TABLE [dbo].[AppEntity] 
ADD  DEFAULT (newid()) FOR [Id]
GO

ALTER TABLE [dbo].[AppEntity] 
ADD  DEFAULT (getdate()) FOR [CreationDate]
GO

ALTER TABLE [dbo].[AppEntity] 
ADD  CONSTRAINT [DF_AppEntity_TokenExpirationDurationHr]  
DEFAULT ((12)) FOR [TokenExpirationDurationHr]
GO

ALTER TABLE [dbo].[RequestLogs] 
ADD  DEFAULT (newid()) FOR [Id]
GO

ALTER TABLE [dbo].[RequestLogs] 
ADD  DEFAULT (getdate()) FOR [CreatedAt]

GO
ALTER TABLE [dbo].[ResponseLogs] 
ADD  DEFAULT (newid()) FOR [Id]
GO

ALTER TABLE [dbo].[WafConditionEntity] 
ADD  DEFAULT (getdate()) FOR [CreationDate]
GO

ALTER TABLE [dbo].[WafRuleEntity] 
ADD  DEFAULT ((0)) FOR [Prioridad]
GO

ALTER TABLE [dbo].[WafRuleEntity] 
ADD  DEFAULT ((1)) FOR [Habilitado]
GO

ALTER TABLE [dbo].[WafRuleEntity] 
ADD  DEFAULT (getdate()) FOR [CreationDate]
GO

ALTER TABLE [dbo].[RequestLogs]  
WITH CHECK ADD  CONSTRAINT [FK_RequestLogs_WafRuleEntity_IdRuleTriggered] 
FOREIGN KEY([IdRuleTriggered])
REFERENCES [dbo].[WafRuleEntity] ([Id])
GO

ALTER TABLE [dbo].[RequestLogs] 
CHECK CONSTRAINT [FK_RequestLogs_WafRuleEntity_IdRuleTriggered]
GO

ALTER TABLE [dbo].[WafConditionEntity] 
WITH CHECK ADD FOREIGN KEY([WafRuleEntityId])
REFERENCES [dbo].[WafRuleEntity] ([Id])
ON DELETE CASCADE
GO

ALTER TABLE [dbo].[WafRuleEntity]  
WITH CHECK ADD  CONSTRAINT [FK_WafRuleEntity_AppId] 
FOREIGN KEY([AppId]) REFERENCES [dbo].[AppEntity] ([Id])
GO

ALTER TABLE [dbo].[WafRuleEntity] 
CHECK CONSTRAINT [FK_WafRuleEntity_AppId]
GO

CREATE PROCEDURE [dbo].[InsertRequestLog]
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
    @ServerVariables NVARCHAR(MAX),
    @RuleTriggered Int,
    @Host Varchar(200)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO RequestLogs (Url, HttpMethod, Headers, QueryString, UserHostAddress, UserAgent, ContentType, ContentLength, RawUrl, ApplicationPath, ActionTaken, ServerVariables, IdRuleTriggered, Host)
    VALUES (@Url, @HttpMethod, @Headers, @QueryString, @UserHostAddress, @UserAgent, @ContentType, @ContentLength, @RawUrl, @ApplicationPath, @ActionTaken, @ServerVariables, @RuleTriggered, @Host);
END;
GO

CREATE PROCEDURE [dbo].[InsertResponseLog]
    @Url NVARCHAR(MAX),
    @HttpMethod NVARCHAR(50),
    @ResponseTime BIGINT,
    @Timestamp DATETIME,
    @ServerVariables NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO ResponseLogs (Url, HttpMethod, ResponseTime, Timestamp, ServerVariables)
    VALUES (@Url, @HttpMethod, @ResponseTime, @Timestamp, @ServerVariables);
END;
GO

CREATE USER [IIS APPPOOL\DefaultAppPool] FOR LOGIN [IIS APPPOOL\DefaultAppPool] WITH DEFAULT_SCHEMA = [dbo];
GO

GRANT EXECUTE ON OBJECT::dbo.InsertRequestLog TO [IIS APPPOOL\DefaultAppPool];
GRANT EXECUTE ON OBJECT::dbo.InsertResponseLog TO [IIS APPPOOL\DefaultAppPool];
GO
ALTER ROLE [db_datareader] ADD MEMBER [IIS APPPOOL\DefaultAppPool]
GO
ALTER ROLE [db_datawriter] ADD MEMBER [IIS APPPOOL\DefaultAppPool]
GO
USE [master]
GO
ALTER DATABASE [GlobalRequests] SET  READ_WRITE 
GO