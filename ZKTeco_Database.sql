-- ============================================================
--  ZKTeco Multi-Company Database
--  SQL Server 2016+
--  Creado: 2026-06-16
-- ============================================================

USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'ZKTecoManager')
    CREATE DATABASE ZKTecoManager;
GO

USE ZKTecoManager;
GO

-- ============================================================
--  1. COMPANY
-- ============================================================
IF OBJECT_ID('dbo.Company', 'U') IS NULL
CREATE TABLE dbo.Company (
    Id          INT             NOT NULL IDENTITY(1,1),
    Name        NVARCHAR(100)   NOT NULL,
    TaxId       NVARCHAR(20)    NULL,
    Address     NVARCHAR(200)   NULL,
    Phone       NVARCHAR(20)    NULL,
    Email       NVARCHAR(100)   NULL,
    IsActive    BIT             NOT NULL CONSTRAINT DF_Company_IsActive    DEFAULT 1,
    CreatedAt   DATETIME2       NOT NULL CONSTRAINT DF_Company_CreatedAt   DEFAULT SYSUTCDATETIME(),
    UpdatedAt   DATETIME2       NOT NULL CONSTRAINT DF_Company_UpdatedAt   DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Company      PRIMARY KEY (Id),
    CONSTRAINT UQ_Company_Name UNIQUE (Name)
);
GO

-- ============================================================
--  2. DEPARTMENT
-- ============================================================
IF OBJECT_ID('dbo.Department', 'U') IS NULL
CREATE TABLE dbo.Department (
    Id          INT             NOT NULL IDENTITY(1,1),
    CompanyId   INT             NOT NULL,
    Name        NVARCHAR(100)   NOT NULL,
    IsActive    BIT             NOT NULL CONSTRAINT DF_Department_IsActive  DEFAULT 1,
    CreatedAt   DATETIME2       NOT NULL CONSTRAINT DF_Department_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt   DATETIME2       NOT NULL CONSTRAINT DF_Department_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Department             PRIMARY KEY (Id),
    CONSTRAINT FK_Department_Company     FOREIGN KEY (CompanyId) REFERENCES dbo.Company (Id),
    CONSTRAINT UQ_Department_CompanyName UNIQUE (CompanyId, Name)
);
GO

-- ============================================================
--  3. EMPLOYEE
-- ============================================================
IF OBJECT_ID('dbo.Employee', 'U') IS NULL
CREATE TABLE dbo.Employee (
    Id              INT             NOT NULL IDENTITY(1,1),
    CompanyId       INT             NOT NULL,
    DepartmentId    INT             NULL,
    EmployeeCode    NVARCHAR(20)    NOT NULL,
    FirstName       NVARCHAR(80)    NOT NULL,
    LastName        NVARCHAR(80)    NOT NULL,
    Position        NVARCHAR(100)   NULL,
    HireDate        DATE            NULL,
    CardNumber      NVARCHAR(20)    NULL,       -- Número de tarjeta RFID
    IsActive        BIT             NOT NULL CONSTRAINT DF_Employee_IsActive  DEFAULT 1,
    CreatedAt       DATETIME2       NOT NULL CONSTRAINT DF_Employee_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2       NOT NULL CONSTRAINT DF_Employee_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Employee            PRIMARY KEY (Id),
    CONSTRAINT FK_Employee_Company    FOREIGN KEY (CompanyId)    REFERENCES dbo.Company (Id),
    CONSTRAINT FK_Employee_Department FOREIGN KEY (DepartmentId) REFERENCES dbo.Department (Id),
    CONSTRAINT UQ_Employee_Code       UNIQUE (CompanyId, EmployeeCode)
);
GO

-- ============================================================
--  4. DEVICE
-- ============================================================
IF OBJECT_ID('dbo.Device', 'U') IS NULL
CREATE TABLE dbo.Device (
    Id              INT             NOT NULL IDENTITY(1,1),
    CompanyId       INT             NOT NULL,
    Name            NVARCHAR(80)    NOT NULL,
    IpAddress       VARCHAR(15)     NOT NULL,
    Port            INT             NOT NULL CONSTRAINT DF_Device_Port     DEFAULT 4370,
    CommPassword    NVARCHAR(20)    NULL,
    SerialNumber    NVARCHAR(50)    NULL,
    Model           NVARCHAR(50)    NULL,
    Location        NVARCHAR(100)   NULL,
    IsActive        BIT             NOT NULL CONSTRAINT DF_Device_IsActive  DEFAULT 1,
    LastSync        DATETIME2       NULL,
    CreatedAt       DATETIME2       NOT NULL CONSTRAINT DF_Device_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2       NOT NULL CONSTRAINT DF_Device_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Device         PRIMARY KEY (Id),
    CONSTRAINT FK_Device_Company FOREIGN KEY (CompanyId) REFERENCES dbo.Company (Id),
    CONSTRAINT UQ_Device_IP      UNIQUE (IpAddress, Port)
);
GO

-- ============================================================
--  5. DEVICE USER  (empleados enrolados en cada reloj)
-- ============================================================
IF OBJECT_ID('dbo.DeviceUser', 'U') IS NULL
CREATE TABLE dbo.DeviceUser (
    Id              INT             NOT NULL IDENTITY(1,1),
    DeviceId        INT             NOT NULL,
    EmployeeId      INT             NOT NULL,
    PinOnDevice     NVARCHAR(20)    NOT NULL,   -- PIN tal como está en el reloj
    CardNumber      NVARCHAR(20)    NULL,        -- Tarjeta registrada en este reloj
    SyncedAt        DATETIME2       NULL,
    CreatedAt       DATETIME2       NOT NULL CONSTRAINT DF_DeviceUser_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_DeviceUser             PRIMARY KEY (Id),
    CONSTRAINT FK_DeviceUser_Device      FOREIGN KEY (DeviceId)   REFERENCES dbo.Device (Id),
    CONSTRAINT FK_DeviceUser_Employee    FOREIGN KEY (EmployeeId) REFERENCES dbo.Employee (Id),
    CONSTRAINT UQ_DeviceUser             UNIQUE (DeviceId, EmployeeId)
);
GO

-- ============================================================
--  6. WORK SHIFT
-- ============================================================
IF OBJECT_ID('dbo.WorkShift', 'U') IS NULL
CREATE TABLE dbo.WorkShift (
    Id                  INT             NOT NULL IDENTITY(1,1),
    CompanyId           INT             NOT NULL,
    Name                NVARCHAR(80)    NOT NULL,
    StartTime           TIME(0)         NOT NULL,
    EndTime             TIME(0)         NOT NULL,
    ToleranceMinutes    INT             NOT NULL CONSTRAINT DF_WorkShift_Tolerance DEFAULT 0,
    IsNightShift        BIT             NOT NULL CONSTRAINT DF_WorkShift_Night     DEFAULT 0,
    IsActive            BIT             NOT NULL CONSTRAINT DF_WorkShift_IsActive  DEFAULT 1,
    CreatedAt           DATETIME2       NOT NULL CONSTRAINT DF_WorkShift_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_WorkShift         PRIMARY KEY (Id),
    CONSTRAINT FK_WorkShift_Company FOREIGN KEY (CompanyId) REFERENCES dbo.Company (Id)
);
GO

-- ============================================================
--  7. EMPLOYEE SHIFT  (historial de turnos por empleado)
-- ============================================================
IF OBJECT_ID('dbo.EmployeeShift', 'U') IS NULL
CREATE TABLE dbo.EmployeeShift (
    Id              INT     NOT NULL IDENTITY(1,1),
    EmployeeId      INT     NOT NULL,
    ShiftId         INT     NOT NULL,
    EffectiveFrom   DATE    NOT NULL,
    EffectiveTo     DATE    NULL,   -- NULL = turno vigente
    CONSTRAINT PK_EmployeeShift          PRIMARY KEY (Id),
    CONSTRAINT FK_EmployeeShift_Employee FOREIGN KEY (EmployeeId) REFERENCES dbo.Employee (Id),
    CONSTRAINT FK_EmployeeShift_Shift    FOREIGN KEY (ShiftId)    REFERENCES dbo.WorkShift (Id)
);
GO

-- ============================================================
--  8. ATTENDANCE LOG  (registro raw del reloj, sin modificar)
-- ============================================================
IF OBJECT_ID('dbo.AttendanceLog', 'U') IS NULL
CREATE TABLE dbo.AttendanceLog (
    Id              BIGINT          NOT NULL IDENTITY(1,1),
    DeviceId        INT             NOT NULL,
    EmployeeId      INT             NULL,        -- NULL si el PIN no matchea ningún empleado
    PinOnDevice     NVARCHAR(20)    NOT NULL,
    PunchTime       DATETIME2       NOT NULL,
    PunchType       TINYINT         NOT NULL,
    -- 0=Check-In  1=Check-Out  2=Break-Out  3=Break-In
    -- 4=Overtime-In  5=Overtime-Out
    VerifyMethod    TINYINT         NOT NULL,
    -- 1=Huella  3=Password  4=Tarjeta RFID  15=Facial  200=Huella+Tarjeta
    WorkCode        NVARCHAR(10)    NULL,
    RawData         NVARCHAR(500)   NULL,        -- Línea original del SDK para auditoría
    IsProcessed     BIT             NOT NULL CONSTRAINT DF_AttLog_IsProcessed DEFAULT 0,
    CreatedAt       DATETIME2       NOT NULL CONSTRAINT DF_AttLog_CreatedAt   DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_AttendanceLog       PRIMARY KEY (Id),
    CONSTRAINT FK_AttLog_Device       FOREIGN KEY (DeviceId)   REFERENCES dbo.Device (Id),
    CONSTRAINT FK_AttLog_Employee     FOREIGN KEY (EmployeeId) REFERENCES dbo.Employee (Id)
);
GO

-- ============================================================
--  9. ATTENDANCE RECORD  (registro calculado por día/empleado)
-- ============================================================
IF OBJECT_ID('dbo.AttendanceRecord', 'U') IS NULL
CREATE TABLE dbo.AttendanceRecord (
    Id              INT             NOT NULL IDENTITY(1,1),
    EmployeeId      INT             NOT NULL,
    WorkDate        DATE            NOT NULL,
    ShiftId         INT             NULL,
    CheckIn         DATETIME2       NULL,
    CheckOut        DATETIME2       NULL,
    HoursWorked     DECIMAL(5,2)    NULL,
    OvertimeHours   DECIMAL(5,2)    NULL CONSTRAINT DF_AttRec_Overtime  DEFAULT 0,
    LateMinutes     INT             NULL CONSTRAINT DF_AttRec_Late      DEFAULT 0,
    Status          TINYINT         NOT NULL CONSTRAINT DF_AttRec_Status DEFAULT 0,
    -- 0=Pendiente  1=Normal  2=Tardanza  3=Falta  4=Justificado  5=Día libre
    Notes           NVARCHAR(200)   NULL,
    ProcessedAt     DATETIME2       NULL,
    CreatedAt       DATETIME2       NOT NULL CONSTRAINT DF_AttRec_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2       NOT NULL CONSTRAINT DF_AttRec_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_AttendanceRecord         PRIMARY KEY (Id),
    CONSTRAINT FK_AttRec_Employee          FOREIGN KEY (EmployeeId) REFERENCES dbo.Employee (Id),
    CONSTRAINT FK_AttRec_Shift             FOREIGN KEY (ShiftId)    REFERENCES dbo.WorkShift (Id),
    CONSTRAINT UQ_AttRec_EmployeeDate      UNIQUE (EmployeeId, WorkDate)
);
GO

-- ============================================================
--  10. INCIDENT  (incidencias, justificantes, permisos)
-- ============================================================
IF OBJECT_ID('dbo.Incident', 'U') IS NULL
CREATE TABLE dbo.Incident (
    Id              INT             NOT NULL IDENTITY(1,1),
    EmployeeId      INT             NOT NULL,
    IncidentDate    DATE            NOT NULL,
    IncidentType    TINYINT         NOT NULL,
    -- 1=Permiso  2=Enfermedad  3=Vacaciones  4=Tardanza justificada
    -- 5=Falta justificada  6=Día compensatorio  7=Otro
    Description     NVARCHAR(300)   NULL,
    ApprovedBy      NVARCHAR(100)   NULL,
    ApprovedAt      DATETIME2       NULL,
    CreatedAt       DATETIME2       NOT NULL CONSTRAINT DF_Incident_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Incident          PRIMARY KEY (Id),
    CONSTRAINT FK_Incident_Employee FOREIGN KEY (EmployeeId) REFERENCES dbo.Employee (Id)
);
GO

-- ============================================================
--  ÍNDICES
-- ============================================================

-- AttendanceLog: consultas por rango de fecha
CREATE NONCLUSTERED INDEX IX_AttLog_PunchTime
    ON dbo.AttendanceLog (PunchTime)
    INCLUDE (EmployeeId, DeviceId, PunchType, VerifyMethod);

-- AttendanceLog: procesar registros pendientes
CREATE NONCLUSTERED INDEX IX_AttLog_IsProcessed
    ON dbo.AttendanceLog (IsProcessed)
    WHERE IsProcessed = 0;

-- AttendanceLog: buscar por tarjeta (VerifyMethod=4)
CREATE NONCLUSTERED INDEX IX_AttLog_VerifyMethod
    ON dbo.AttendanceLog (VerifyMethod, PunchTime)
    INCLUDE (EmployeeId, PinOnDevice);

-- AttendanceRecord: reporte mensual por empleado
CREATE NONCLUSTERED INDEX IX_AttRec_EmployeeDate
    ON dbo.AttendanceRecord (EmployeeId, WorkDate);

-- Employee: búsqueda por empresa
CREATE NONCLUSTERED INDEX IX_Employee_CompanyId
    ON dbo.Employee (CompanyId)
    INCLUDE (FirstName, LastName, EmployeeCode, CardNumber, IsActive);

-- Employee: búsqueda por número de tarjeta
CREATE NONCLUSTERED INDEX IX_Employee_CardNumber
    ON dbo.Employee (CardNumber)
    WHERE CardNumber IS NOT NULL;

-- DeviceUser: sincronización por dispositivo
CREATE NONCLUSTERED INDEX IX_DeviceUser_DeviceId
    ON dbo.DeviceUser (DeviceId)
    INCLUDE (EmployeeId, PinOnDevice, CardNumber);

-- Incident: consultas por empleado y fecha
CREATE NONCLUSTERED INDEX IX_Incident_EmployeeDate
    ON dbo.Incident (EmployeeId, IncidentDate);

GO

-- ============================================================
--  DATOS INICIALES DE PRUEBA
-- ============================================================

INSERT INTO dbo.Company (Name, TaxId, Address, Phone)
VALUES
    ('Difiesta', '123456789', 'Av. Principal 100',    '5550001'),
    ('Pafisa',   '987654321', 'Calle Secundaria 200', '5550002');

INSERT INTO dbo.Department (CompanyId, Name)
VALUES
    (1, 'Administración'),
    (1, 'Operaciones'),
    (1, 'Recursos Humanos'),
    (2, 'Administración'),
    (2, 'Producción');

INSERT INTO dbo.WorkShift (CompanyId, Name, StartTime, EndTime, ToleranceMinutes, IsNightShift)
VALUES
    (1, 'Turno Mañana', '07:00', '15:00', 10, 0),
    (1, 'Turno Tarde',  '15:00', '23:00', 10, 0),
    (2, 'Turno Mañana', '08:00', '17:00', 10, 0),
    (2, 'Turno Noche',  '22:00', '06:00', 15, 1);

GO

PRINT '✔ Base de datos ZKTecoManager creada correctamente.';
GO
