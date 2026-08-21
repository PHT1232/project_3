CREATE TABLE [ApplicationUser] (
  [UserId] varchar(255) PRIMARY KEY,
  [Role] varchar(50)
)
GO

CREATE TABLE [Category] (
  [Id] int PRIMARY KEY,
  [Name] varchar(255),
  [Priority] int,
  [CreatedAt] datetime,
  [UpdatedAt] datetime,
  [IsDeleted] binary
)
GO

CREATE TABLE [SubCategory] (
  [Id] int PRIMARY KEY,
  [CategoryId] int,
  [Name] varchar(255),
  [CreatedAt] datetime,
  [UpdatedAt] datetime,
  [IsDeleted] binary
)
GO

CREATE TABLE [Stationery] (
  [Id] int PRIMARY KEY,
  [Name] varchar(255),
  [BrandName] varcher(255),
  [SubCategoryId] int,
  [price] float,
  [CreatedAt] datetime,
  [UpdatedAt] datetime,
  [IsDeleted] binary
)
GO

CREATE TABLE [Department] (
  [Id] int PRIMARY KEY,
  [Name] varchar(255),
  [CreatedAt] datetime,
  [UpdatedAt] datetime,
  [IsDeleted] binary
)
GO

CREATE TABLE [EmployeeDepartment] (
  [UserId] varchar(255),
  [DepartmentId] int,
  [Role] varchar(255),
  [MaximumAmount] decimal,
  [MaximumItems] int,
  [CreatedAt] datetime,
  [UpdatedAt] datetime,
  [IsDeleted] binary,
  PRIMARY KEY ([UserId], [DepartmentId])
)
GO

CREATE TABLE [Storage] (
  [Id] int PRIMARY KEY,
  [Name] varchar(255),
  [Location] varchar(255),
  [SubCategoryId] int,
  [CurrentQuantity] int,
  [CreatedAt] datetime,
  [UpdatedAt] datetime,
  [IsDeleted] binary
)
GO

CREATE TABLE [StationeryStorage] (
  [StationeryId] int,
  [StorageId] int,
  [CreatedAt] datetime,
  [UpdatedAt] datetime,
  [IsDeleted] binary,
  PRIMARY KEY ([StationeryId], [StorageId])
)
GO

CREATE TABLE [DepartmentStorage] (
  [DepartmentId] int,
  [StorageId] int,
  [MaxQuantity] int,
  [MinQuantity] int,
  [CreatedAt] datetime,
  [UpdatedAt] datetime,
  [IsDeleted] binary,
  PRIMARY KEY ([DepartmentId], [StorageId])
)
GO

CREATE TABLE [StationeryRequest] (
  [Id] int PRIMARY KEY,
  [StorageId] int,
  [TotalAmount] decimal,
  [RequestedUserId] varchar(255),
  [Type] enum,
  [Notes] varchar(255),
  [Status] enum,
  [CreatedAt] datetime,
  [UpdatedAt] datetime,
  [IsDeleted] binary
)
GO

CREATE TABLE [StationeryRequestPasson] (
  [Id] int PRIMARY KEY,
  [PrevRequestId] int,
  [CurrentRequesterId] varchar(255),
  [NextApproverId] varchar(255),
  [CreatedAt] datetime,
  [UpdatedAt] datetime,
  [IsDeleted] binary
)
GO

CREATE TABLE [RequestDetail] (
  [StationeryId] int,
  [RequestId] int,
  [Quantity] int,
  [SubTotal] decimal,
  [CreatedAt] datetime,
  [UpdatedAt] datetime,
  [IsDeleted] binary,
  PRIMARY KEY ([StationeryId], [RequestId])
)
GO

CREATE TABLE [ApprovalHistory] (
  [Id] int PRIMARY KEY,
  [StationeryRequestId] int,
  [ApproverId] varchar(255),
  [Notes] varchar(500),
  [CreatedAt] datetime,
  [UpdatedAt] datetime,
  [IsDeleted] binary
)
GO

CREATE TABLE [EmployeeStorage] (
  [EmployeeId] varchar(255),
  [StorageId] int,
  [CreatedAt] datetime,
  [UpdatedAt] datetime,
  [IsDeleted] binary
)
GO

CREATE TABLE [Notification] (
  [Id] int PRIMARY KEY,
  [EmployeeId] varchar(255),
  [Message] varchar(255),
  [Type] enum,
  [IsRead] bool,
  [CreatedAt] datetime,
  [UpdatedAt] datetime,
  [IsDeleted] binary
)
GO

ALTER TABLE [EmployeeDepartment] ADD FOREIGN KEY ([UserId]) REFERENCES [ApplicationUser] ([UserId])
GO

ALTER TABLE [EmployeeDepartment] ADD FOREIGN KEY ([DepartmentId]) REFERENCES [Department] ([Id])
GO

ALTER TABLE [DepartmentStorage] ADD FOREIGN KEY ([StorageId]) REFERENCES [Storage] ([Id])
GO

ALTER TABLE [DepartmentStorage] ADD FOREIGN KEY ([DepartmentId]) REFERENCES [Department] ([Id])
GO

ALTER TABLE [SubCategory] ADD FOREIGN KEY ([CategoryId]) REFERENCES [Category] ([Id])
GO

ALTER TABLE [SubCategory] ADD FOREIGN KEY ([Id]) REFERENCES [Storage] ([SubCategoryId])
GO

ALTER TABLE [SubCategory] ADD FOREIGN KEY ([Id]) REFERENCES [Stationery] ([SubCategoryId])
GO

ALTER TABLE [Stationery] ADD FOREIGN KEY ([Id]) REFERENCES [StationeryStorage] ([StationeryId])
GO

ALTER TABLE [Storage] ADD FOREIGN KEY ([Id]) REFERENCES [StationeryStorage] ([StorageId])
GO

ALTER TABLE [Stationery] ADD FOREIGN KEY ([Id]) REFERENCES [RequestDetail] ([StationeryId])
GO

ALTER TABLE [RequestDetail] ADD FOREIGN KEY ([RequestId]) REFERENCES [StationeryRequest] ([Id])
GO

ALTER TABLE [Storage] ADD FOREIGN KEY ([Id]) REFERENCES [StationeryRequest] ([StorageId])
GO

ALTER TABLE [ApplicationUser] ADD FOREIGN KEY ([UserId]) REFERENCES [StationeryRequest] ([RequestedUserId])
GO

ALTER TABLE [StationeryRequest] ADD FOREIGN KEY ([Id]) REFERENCES [ApprovalHistory] ([StationeryRequestId])
GO

ALTER TABLE [ApplicationUser] ADD FOREIGN KEY ([UserId]) REFERENCES [ApprovalHistory] ([ApproverId])
GO

ALTER TABLE [StationeryRequest] ADD FOREIGN KEY ([Id]) REFERENCES [StationeryRequestPasson] ([PrevRequestId])
GO

ALTER TABLE [ApplicationUser] ADD FOREIGN KEY ([UserId]) REFERENCES [StationeryRequestPasson] ([CurrentRequesterId])
GO

ALTER TABLE [ApplicationUser] ADD FOREIGN KEY ([UserId]) REFERENCES [StationeryRequestPasson] ([NextApproverId])
GO

ALTER TABLE [ApplicationUser] ADD FOREIGN KEY ([UserId]) REFERENCES [Notification] ([EmployeeId])
GO

ALTER TABLE [Storage] ADD FOREIGN KEY ([Id]) REFERENCES [EmployeeStorage] ([StorageId])
GO

ALTER TABLE [ApplicationUser] ADD FOREIGN KEY ([UserId]) REFERENCES [EmployeeStorage] ([EmployeeId])
GO
