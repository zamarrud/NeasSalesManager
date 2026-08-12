-- ============================================================================
-- DATABASE CREATION AND INITIALIZATION SCRIPT (ENTERPRISE EDITION)
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
    CreatedUtc  DATETIME2(7)      NOT NULL CONSTRAINT DF_District_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    
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
    CreatedUtc    DATETIME2(7)      NOT NULL CONSTRAINT DF_Salesperson_CreatedUtc DEFAULT (SYSUTCDATETIME()),

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
    CreatedUtc  DATETIME2(7)      NOT NULL CONSTRAINT DF_Store_CreatedUtc DEFAULT (SYSUTCDATETIME()),

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
    AssignedUtc   DATETIME2(7) NOT NULL CONSTRAINT DF_DistrictSalesperson_AssignedUtc DEFAULT (SYSUTCDATETIME()),

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
-- 3. TRIGGERS: MANDATORY SINGLE PRIMARY ENFORCEMENT
-- ============================================================================

-- Enforce Minimum 1 Primary Salesperson Per District (Trigger)
CREATE OR ALTER TRIGGER dbo.trg_EnforceSinglePrimaryPerDistrict
ON dbo.DistrictSalesperson
AFTER UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    -- Ignore if no rows were affected
    IF NOT EXISTS (SELECT 1 FROM inserted) AND NOT EXISTS (SELECT 1 FROM deleted)
        RETURN;

    -- Check if any affected district has 0 primary salespersons after the statement completes
    IF EXISTS (
        SELECT d.DistrictId
        FROM dbo.District d
        LEFT JOIN dbo.DistrictSalesperson ds 
            ON d.DistrictId = ds.DistrictId AND ds.IsPrimary = 1
        WHERE d.DistrictId IN (
            SELECT DistrictId FROM inserted
            UNION
            SELECT DistrictId FROM deleted
        )
        GROUP BY d.DistrictId
        HAVING COUNT(ds.SalespersonId) = 0
    )
    BEGIN
        RAISERROR ('Business Rule Violation: Every district MUST have exactly ONE primary salesperson.', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END
END;
GO

-- ============================================================================
-- 4. STORED PROCEDURES
-- ============================================================================

-- ----------------------------------------------------------------------------
-- SP: Upsert or Assign Salesperson to a District
-- Handles primary toggles and role swaps inside an explicit transaction
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_AssignSalespersonToDistrict
    @DistrictId INT,
    @SalespersonId INT,
    @IsPrimary BIT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRANSACTION;
    BEGIN TRY
        -- Guard 1: Verify District and Salesperson exist
        IF NOT EXISTS (SELECT 1 FROM dbo.District WHERE DistrictId = @DistrictId)
        BEGIN
            RAISERROR ('Invalid District ID.', 16, 1);
            ROLLBACK TRANSACTION;
            RETURN;
        END

        IF NOT EXISTS (SELECT 1 FROM dbo.Salesperson WHERE SalespersonId = @SalespersonId)
        BEGIN
            RAISERROR ('Invalid Salesperson ID.', 16, 1);
            ROLLBACK TRANSACTION;
            RETURN;
        END

        -- Guard 2: Prevent demoting primary salesperson directly without promoting another
        IF @IsPrimary = 0 AND EXISTS (
            SELECT 1 FROM dbo.DistrictSalesperson 
            WHERE DistrictId = @DistrictId AND SalespersonId = @SalespersonId AND IsPrimary = 1
        )
        BEGIN
            RAISERROR ('Cannot demote primary salesperson directly. Assign another salesperson as primary to swap roles.', 16, 1);
            ROLLBACK TRANSACTION;
            RETURN;
        END

        -- Step 1: Ensure target salesperson exists in the district table
        IF NOT EXISTS (
            SELECT 1 FROM dbo.DistrictSalesperson 
            WHERE DistrictId = @DistrictId AND SalespersonId = @SalespersonId
        )
        BEGIN
            INSERT INTO dbo.DistrictSalesperson (DistrictId, SalespersonId, IsPrimary, AssignedUtc)
            VALUES (@DistrictId, @SalespersonId, 0, SYSUTCDATETIME());
        END

        -- Step 2: Atomic Swap in 1 single UPDATE query
        IF @IsPrimary = 1
        BEGIN
            UPDATE dbo.DistrictSalesperson
            SET IsPrimary = CASE 
                    WHEN SalespersonId = @SalespersonId THEN 1 
                    ELSE 0 
                END,
                AssignedUtc = SYSUTCDATETIME()
            WHERE DistrictId = @DistrictId 
              AND (SalespersonId = @SalespersonId OR IsPrimary = 1);
        END

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
-- SP: Atomic Creation of District + Mandatory Primary Salesperson
-- ----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_CreateDistrict
    @Name NVARCHAR(100),
    @PrimarySalespersonId INT,
    @NewDistrictId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    BEGIN TRY
        -- 1. Insert District
        INSERT INTO dbo.District (Name)
        VALUES (@Name);

        SET @NewDistrictId = SCOPE_IDENTITY();

        -- 2. Immediately assign the mandatory Primary Salesperson
        INSERT INTO dbo.DistrictSalesperson (DistrictId, SalespersonId, IsPrimary)
        VALUES (@NewDistrictId, @PrimarySalespersonId, 1);

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
CREATE OR ALTER PROCEDURE dbo.sp_GetDistrictDetails
    @DistrictId INT
AS
BEGIN
    SET NOCOUNT ON;

    -- Result Set 1: District
    SELECT 
        DistrictId, 
        Name
    FROM dbo.District
    WHERE DistrictId = @DistrictId;

    -- Result Set 2: Stores
    SELECT 
        StoreId, 
        Name, 
        Address
    FROM dbo.Store
    WHERE DistrictId = @DistrictId;

    -- Result Set 3: Salespersons
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

-- Stored Procedure for explicit removal
CREATE OR ALTER PROCEDURE dbo.sp_RemoveSalespersonFromDistrict
    @DistrictId INT,
    @SalespersonId INT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRANSACTION;
    BEGIN TRY
        -- Guard: Check if salesperson is Primary
        IF EXISTS (
            SELECT 1 
            FROM dbo.DistrictSalesperson 
            WHERE DistrictId = @DistrictId AND SalespersonId = @SalespersonId AND IsPrimary = 1
        )
        BEGIN
            RAISERROR ('Cannot remove the primary salesperson. Reassign the primary role to another salesperson first.', 16, 1);
            ROLLBACK TRANSACTION;
            RETURN;
        END

        -- Execute Removal
        DELETE FROM dbo.DistrictSalesperson
        WHERE DistrictId = @DistrictId AND SalespersonId = @SalespersonId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- ============================================================================
-- 5. SEED DATA GENERATION
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

-- Assign Salespersons using stored procedure to respect triggers
EXEC dbo.sp_AssignSalespersonToDistrict @DistrictId = 1, @SalespersonId = 1, @IsPrimary = 1;
EXEC dbo.sp_AssignSalespersonToDistrict @DistrictId = 1, @SalespersonId = 2, @IsPrimary = 0;

EXEC dbo.sp_AssignSalespersonToDistrict @DistrictId = 2, @SalespersonId = 1, @IsPrimary = 1;
EXEC dbo.sp_AssignSalespersonToDistrict @DistrictId = 2, @SalespersonId = 4, @IsPrimary = 0;

EXEC dbo.sp_AssignSalespersonToDistrict @DistrictId = 3, @SalespersonId = 3, @IsPrimary = 1;

EXEC dbo.sp_AssignSalespersonToDistrict @DistrictId = 4, @SalespersonId = 4, @IsPrimary = 1;
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