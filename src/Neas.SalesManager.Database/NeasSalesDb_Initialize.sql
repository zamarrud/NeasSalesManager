-- ============================================================================
-- DATABASE CREATION AND INITIALIZATION SCRIPT (FIXED)
-- Project: Neas Energy District Sales Manager
-- Engine: Microsoft SQL Server 2019+
-- ============================================================================

USE [master];
GO

IF EXISTS (SELECT name FROM sys.databases WHERE name = N'NeasSalesDb')
BEGIN
    ALTER DATABASE [NeasSalesDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [NeasSalesDb];
END
GO

CREATE DATABASE [NeasSalesDb];
GO

USE [NeasSalesDb];
GO

-- Set default isolation level & ANSI compliance options
ALTER DATABASE [NeasSalesDb] SET READ_COMMITTED_SNAPSHOT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================================
-- 1. SCHEMAS & TABLES CREATION
-- ============================================================================

-- District Table
CREATE TABLE dbo.District (
    DistrictId  INT IDENTITY(1,1) NOT NULL,
    Name        NVARCHAR(100)     NOT NULL,
    CreatedUtc  DATETIME2(7)      NOT NULL CONSTRAINT DF_District_CreatedUtc DEFAULT (GETUTCDATE()),
    
    CONSTRAINT PK_District PRIMARY KEY CLUSTERED (DistrictId ASC),
    CONSTRAINT UQ_District_Name UNIQUE NONCLUSTERED (Name ASC)
);
GO

-- Salesperson Table
CREATE TABLE dbo.Salesperson (
    SalespersonId INT IDENTITY(1,1) NOT NULL,
    FirstName     NVARCHAR(50)      NOT NULL,
    LastName      NVARCHAR(50)      NOT NULL,
    Email         NVARCHAR(100)     NOT NULL,
    CreatedUtc    DATETIME2(7)      NOT NULL CONSTRAINT DF_Salesperson_CreatedUtc DEFAULT (GETUTCDATE()),

    CONSTRAINT PK_Salesperson PRIMARY KEY CLUSTERED (SalespersonId ASC),
    CONSTRAINT UQ_Salesperson_Email UNIQUE NONCLUSTERED (Email ASC)
);
GO

-- Store Table
CREATE TABLE dbo.Store (
    StoreId     INT IDENTITY(1,1) NOT NULL,
    DistrictId  INT               NOT NULL,
    Name        NVARCHAR(150)     NOT NULL,
    Address     NVARCHAR(250)     NULL,
    CreatedUtc  DATETIME2(7)      NOT NULL CONSTRAINT DF_Store_CreatedUtc DEFAULT (GETUTCDATE()),

    CONSTRAINT PK_Store PRIMARY KEY CLUSTERED (StoreId ASC),
    CONSTRAINT FK_Store_District FOREIGN KEY (DistrictId) 
        REFERENCES dbo.District (DistrictId) 
        ON DELETE CASCADE
);
GO

-- DistrictSalesperson Junction Table
CREATE TABLE dbo.DistrictSalesperson (
    DistrictId    INT          NOT NULL,
    SalespersonId INT          NOT NULL,
    IsPrimary     BIT          NOT NULL CONSTRAINT DF_DistrictSalesperson_IsPrimary DEFAULT (0),
    AssignedUtc   DATETIME2(7) NOT NULL CONSTRAINT DF_DistrictSalesperson_AssignedUtc DEFAULT (GETUTCDATE()),

    CONSTRAINT PK_DistrictSalesperson PRIMARY KEY CLUSTERED (DistrictId ASC, SalespersonId ASC),
    CONSTRAINT FK_DistrictSalesperson_District FOREIGN KEY (DistrictId) 
        REFERENCES dbo.District (DistrictId) 
        ON DELETE CASCADE,
    CONSTRAINT FK_DistrictSalesperson_Salesperson FOREIGN KEY (SalespersonId) 
        REFERENCES dbo.Salesperson (SalespersonId) 
        ON DELETE CASCADE
);
GO

-- ============================================================================
-- 2. INDEXES & BUSINESS RULE CONSTRAINTS
-- ============================================================================

-- Business Rule Constraint: Enforce maximum of ONE primary salesperson per district
CREATE UNIQUE NONCLUSTERED INDEX UX_DistrictSalesperson_SinglePrimary 
ON dbo.DistrictSalesperson(DistrictId ASC) 
WHERE IsPrimary = 1;
GO

-- Non-Clustered Performance Indexes for Foreign Key Navigation & Joins
CREATE NONCLUSTERED INDEX IX_Store_DistrictId 
ON dbo.Store(DistrictId ASC) 
INCLUDE (Name, Address);
GO

CREATE NONCLUSTERED INDEX IX_DistrictSalesperson_SalespersonId 
ON dbo.DistrictSalesperson(SalespersonId ASC) 
INCLUDE (DistrictId, IsPrimary);
GO

-- ============================================================================
-- 3. STORED PROCEDURES (CORRECTED SYNTAX)
-- ============================================================================

-- ----------------------------------------------------------------------------
-- SP: Upsert or Assign Salesperson to a District
-- Handles primary assignment toggles atomically inside an explicit transaction
-- ----------------------------------------------------------------------------
CREATE PROCEDURE dbo.sp_AssignSalespersonToDistrict
    @DistrictId    INT,
    @SalespersonId INT,
    @IsPrimary     BIT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Check if District and Salesperson exist
        IF NOT EXISTS (SELECT 1 FROM dbo.District WHERE DistrictId = @DistrictId)
        BEGIN
            RAISERROR('District with ID %d does not exist.', 16, 1, @DistrictId);
        END

        IF NOT EXISTS (SELECT 1 FROM dbo.Salesperson WHERE SalespersonId = @SalespersonId)
        BEGIN
            RAISERROR('Salesperson with ID %d does not exist.', 16, 1, @SalespersonId);
        END

        -- If promoting to Primary, unset existing primary salesperson for this district
        IF @IsPrimary = 1
        BEGIN
            UPDATE dbo.DistrictSalesperson 
            SET IsPrimary = 0 
            WHERE DistrictId = @DistrictId AND IsPrimary = 1;
        END

        -- Atomic Upsert using MERGE
        MERGE INTO dbo.DistrictSalesperson AS Target
        USING (SELECT @DistrictId AS DistrictId, @SalespersonId AS SalespersonId) AS Source
        ON Target.DistrictId = Source.DistrictId AND Target.SalespersonId = Source.SalespersonId
        WHEN MATCHED THEN
            UPDATE SET IsPrimary = @IsPrimary, AssignedUtc = GETUTCDATE()
        WHEN NOT MATCHED THEN
            INSERT (DistrictId, SalespersonId, IsPrimary, AssignedUtc)
            VALUES (@DistrictId, @SalespersonId, @IsPrimary, GETUTCDATE());

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        THROW;
    END CATCH
END;
GO

-- ----------------------------------------------------------------------------
-- SP: Retrieve Full Details for a Single District
-- ----------------------------------------------------------------------------
CREATE PROCEDURE dbo.sp_GetDistrictDetails
        @DistrictId INT
AS
BEGIN
    SET NOCOUNT ON;

    -- Result Set 1: District (Matches DistrictSummaryDto)
    SELECT 
        DistrictId, 
        Name
    FROM dbo.District
    WHERE DistrictId = @DistrictId;

    -- Result Set 2: Stores (Matches StoreDto)
    SELECT 
        StoreId, 
        Name, 
        Address
    FROM dbo.Store
    WHERE DistrictId = @DistrictId;

    -- Result Set 3: Salespersons (Matches SalespersonDto)
    SELECT 
        s.SalespersonId, 
        s.FirstName, 
        s.LastName, 
        s.Email, 
        ds.IsPrimary
    FROM dbo.DistrictSalesperson ds
    INNER JOIN dbo.Salesperson s ON ds.SalespersonId = s.SalespersonId
    WHERE ds.DistrictId = @DistrictId;
END;
GO

-- ============================================================================
-- 4. SEED DATA GENERATION
-- ============================================================================

BEGIN TRANSACTION;

-- Seed Districts
INSERT INTO dbo.District (Name) VALUES 
(N'North Denmark'),
(N'Southern Denmark'),
(N'Capital Region'),
(N'Central Denmark');

-- Seed Salespersons
INSERT INTO dbo.Salesperson (FirstName, LastName, Email) VALUES 
(N'Mads', N'Mikkelsen', N'mads.mikkelsen@neasenergy.com'),
(N'Freja', N'Lind', N'freja.lind@neasenergy.com'),
(N'Lars', N'Nielsen', N'lars.nielsen@neasenergy.com'),
(N'Astrid', N'Poulsen', N'astrid.poulsen@neasenergy.com');

-- Seed Stores
INSERT INTO dbo.Store (DistrictId, Name, Address) VALUES 
(1, N'Aalborg SuperStore', N'Hobrovej 42, 9000 Aalborg'),
(1, N'Nørresundby Retail', N'Vestergade 10, 9400 Nørresundby'),
(2, N'Odense Central Store', N'Vestergade 15, 5000 Odense'),
(2, N'Svendborg Commerce', N'Gerritsgade 22, 5700 Svendborg'),
(3, N'Copenhagen Flagship', N'Strøget 1, 1100 København K'),
(3, N'Frederiksberg Center', N'Falkoner Allé 21, 2000 Frederiksberg'),
(4, N'Aarhus Main Hub', N'Ryesgade 5, 8000 Aarhus C');

COMMIT TRANSACTION;
GO

-- Assign Salespersons using the Stored Procedure
EXEC dbo.sp_AssignSalespersonToDistrict @DistrictId = 1, @SalespersonId = 1, @IsPrimary = 1;
EXEC dbo.sp_AssignSalespersonToDistrict @DistrictId = 1, @SalespersonId = 2, @IsPrimary = 0;

EXEC dbo.sp_AssignSalespersonToDistrict @DistrictId = 2, @SalespersonId = 1, @IsPrimary = 1;
EXEC dbo.sp_AssignSalespersonToDistrict @DistrictId = 2, @SalespersonId = 4, @IsPrimary = 0;

EXEC dbo.sp_AssignSalespersonToDistrict @DistrictId = 3, @SalespersonId = 3, @IsPrimary = 1;
GO

-- ============================================================================
-- VERIFICATION QUERIES
-- ============================================================================

SELECT 
    d.DistrictId,
    d.Name AS DistrictName,
    COUNT(DISTINCT s.StoreId) AS TotalStores,
    COUNT(DISTINCT ds.SalespersonId) AS TotalSalespersons,
    ISNULL(MAX(CAST(ds.IsPrimary AS INT)), 0) AS HasPrimarySalesperson
FROM dbo.District d
LEFT JOIN dbo.Store s ON d.DistrictId = s.DistrictId
LEFT JOIN dbo.DistrictSalesperson ds ON d.DistrictId = ds.DistrictId
GROUP BY d.DistrictId, d.Name
ORDER BY d.DistrictId;

EXEC dbo.sp_GetDistrictDetails @DistrictId = 1;
GO