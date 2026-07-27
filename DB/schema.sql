CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

START TRANSACTION;
ALTER DATABASE CHARACTER SET utf8mb4;

CREATE TABLE `AlertTypes` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Name` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_AlertTypes` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `AudioFiles` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `FileName` longtext CHARACTER SET utf8mb4 NOT NULL,
    `FilePath` longtext CHARACTER SET utf8mb4 NOT NULL,
    `FileSize` bigint NOT NULL,
    CONSTRAINT `PK_AudioFiles` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `AuditLogs` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Actor` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Action` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Module` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Timestamp` datetime(6) NOT NULL,
    `EntityData` json NOT NULL,
    CONSTRAINT `PK_AuditLogs` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `NotificationTemplates` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Name` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Subject` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Message` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_NotificationTemplates` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Privileges` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Name` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_Privileges` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Roles` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Name` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_Roles` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `SirenGroups` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Name` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Description` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_SirenGroups` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Users` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Username` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Password` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Enabled` tinyint(1) NOT NULL,
    `Created` datetime(6) NOT NULL,
    `LastLogin` datetime(6) NULL,
    `TempPassword` varchar(255) NULL,
    CONSTRAINT `PK_Users` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `CommandConfigs` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Name` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Description` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CommandType` longtext CHARACTER SET utf8mb4 NOT NULL,
    `AudioId` char(36) COLLATE ascii_general_ci NULL,
    `Duration` int NOT NULL,
    CONSTRAINT `PK_CommandConfigs` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_CommandConfigs_AudioFiles_AudioId` FOREIGN KEY (`AudioId`) REFERENCES `AudioFiles` (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `AlertRules` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `AlertTypeId` char(36) COLLATE ascii_general_ci NOT NULL,
    `Priority` longtext CHARACTER SET utf8mb4 NOT NULL,
    `TemplateId` char(36) COLLATE ascii_general_ci NULL,
    `Active` tinyint(1) NOT NULL,
    CONSTRAINT `PK_AlertRules` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_AlertRules_AlertTypes_AlertTypeId` FOREIGN KEY (`AlertTypeId`) REFERENCES `AlertTypes` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_AlertRules_NotificationTemplates_TemplateId` FOREIGN KEY (`TemplateId`) REFERENCES `NotificationTemplates` (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `RolePrivileges` (
    `PrivilegesId` char(36) COLLATE ascii_general_ci NOT NULL,
    `RolesId` char(36) COLLATE ascii_general_ci NOT NULL,
    CONSTRAINT `PK_RolePrivileges` PRIMARY KEY (`PrivilegesId`, `RolesId`),
    CONSTRAINT `FK_RolePrivileges_Privileges_PrivilegesId` FOREIGN KEY (`PrivilegesId`) REFERENCES `Privileges` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_RolePrivileges_Roles_RolesId` FOREIGN KEY (`RolesId`) REFERENCES `Roles` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `SirenDevices` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Name` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Description` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Address` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Lat` double NOT NULL,
    `Lng` double NOT NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Ip` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Redundant` tinyint(1) NOT NULL,
    `GroupId` char(36) COLLATE ascii_general_ci NULL,
    CONSTRAINT `PK_SirenDevices` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_SirenDevices_SirenGroups_GroupId` FOREIGN KEY (`GroupId`) REFERENCES `SirenGroups` (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `UserProfiles` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `UserId` char(36) COLLATE ascii_general_ci NOT NULL,
    `FirstName` longtext CHARACTER SET utf8mb4 NOT NULL,
    `LastName` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Email` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_UserProfiles` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_UserProfiles_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `UserRoles` (
    `RolesId` char(36) COLLATE ascii_general_ci NOT NULL,
    `UsersId` char(36) COLLATE ascii_general_ci NOT NULL,
    CONSTRAINT `PK_UserRoles` PRIMARY KEY (`RolesId`, `UsersId`),
    CONSTRAINT `FK_UserRoles_Roles_RolesId` FOREIGN KEY (`RolesId`) REFERENCES `Roles` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_UserRoles_Users_UsersId` FOREIGN KEY (`UsersId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `SirenDetails` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `SirenDeviceId` char(36) COLLATE ascii_general_ci NOT NULL,
    `FirmwareVersion` longtext CHARACTER SET utf8mb4 NOT NULL,
    `HardwareModel` longtext CHARACTER SET utf8mb4 NOT NULL,
    `LastHealthCheck` datetime(6) NOT NULL,
    CONSTRAINT `PK_SirenDetails` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_SirenDetails_SirenDevices_SirenDeviceId` FOREIGN KEY (`SirenDeviceId`) REFERENCES `SirenDevices` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

INSERT INTO `Users` (`Id`, `Created`, `Enabled`, `LastLogin`, `Password`, `Username`)
VALUES ('00000000-0000-0000-0000-000000000001', TIMESTAMP '2026-05-19 07:57:22', TRUE, NULL, 'admin123', 'admin');

CREATE UNIQUE INDEX `IX_AlertRules_AlertTypeId` ON `AlertRules` (`AlertTypeId`);

CREATE INDEX `IX_AlertRules_TemplateId` ON `AlertRules` (`TemplateId`);

CREATE INDEX `IX_CommandConfigs_AudioId` ON `CommandConfigs` (`AudioId`);

CREATE INDEX `IX_RolePrivileges_RolesId` ON `RolePrivileges` (`RolesId`);

CREATE UNIQUE INDEX `IX_SirenDetails_SirenDeviceId` ON `SirenDetails` (`SirenDeviceId`);

CREATE INDEX `IX_SirenDevices_GroupId` ON `SirenDevices` (`GroupId`);

CREATE UNIQUE INDEX `IX_UserProfiles_UserId` ON `UserProfiles` (`UserId`);

CREATE INDEX `IX_UserRoles_UsersId` ON `UserRoles` (`UsersId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260519075723_InitialCreate', '9.0.0');

ALTER TABLE `Users` ADD `IsFirstTimeLogin` tinyint(1) NOT NULL DEFAULT FALSE;

ALTER TABLE `Users` ADD `Role` longtext CHARACTER SET utf8mb4 NOT NULL;

UPDATE `Users` SET `Created` = TIMESTAMP '2026-05-19 08:46:04', `IsFirstTimeLogin` = FALSE, `Role` = 'Admin'
WHERE `Id` = '00000000-0000-0000-0000-000000000001';
SELECT ROW_COUNT();


INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260519084605_AddUserRoleAndFirstTimeFlag', '9.0.0');

ALTER TABLE `SirenGroups` ADD `Color` longtext CHARACTER SET utf8mb4 NOT NULL;

ALTER TABLE `SirenGroups` ADD `Shape` longtext CHARACTER SET utf8mb4 NOT NULL;

UPDATE `Users` SET `Created` = TIMESTAMP '2026-05-19 13:26:06'
WHERE `Id` = '00000000-0000-0000-0000-000000000001';
SELECT ROW_COUNT();


INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260519132607_AddColorAndShapeToSirenGroup', '9.0.0');

ALTER TABLE `SirenDevices` ADD `AddressCode` longtext CHARACTER SET utf8mb4 NOT NULL;

ALTER TABLE `SirenDevices` ADD `AreaCode` longtext CHARACTER SET utf8mb4 NOT NULL;

UPDATE `Users` SET `Created` = TIMESTAMP '2026-05-20 05:37:59'
WHERE `Id` = '00000000-0000-0000-0000-000000000001';
SELECT ROW_COUNT();


INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260520053800_AddAreaAndAddressCodeToSirenDevice', '9.0.0');

ALTER TABLE `CommandConfigs` ADD `Color` longtext CHARACTER SET utf8mb4 NOT NULL;

ALTER TABLE `CommandConfigs` ADD `CommandHex` int NOT NULL DEFAULT 0;

ALTER TABLE `CommandConfigs` ADD `ExpectedResponseBytes` int NOT NULL DEFAULT 0;

ALTER TABLE `CommandConfigs` ADD `IsEnabled` tinyint(1) NOT NULL DEFAULT FALSE;

INSERT INTO `CommandConfigs` (`Id`, `AudioId`, `Color`, `CommandHex`, `CommandType`, `Description`, `Duration`, `ExpectedResponseBytes`, `IsEnabled`, `Name`)
VALUES ('00000000-0000-0000-0001-000000000001', NULL, 'Blue', 0, 'Clear', 'Clears any event in progress.', 0, 0, TRUE, 'Clear'),
('00000000-0000-0000-0001-000000000002', NULL, 'Red', 1, 'Wail', 'Wail tone warning.', 0, 4, TRUE, 'Wail'),
('00000000-0000-0000-0001-000000000003', NULL, 'Red', 2, 'Attack', 'Attack tone warning.', 0, 4, TRUE, 'Attack'),
('00000000-0000-0000-0001-000000000004', NULL, 'Orange', 3, 'Alert', 'Alert tone warning.', 0, 4, TRUE, 'Alert'),
('00000000-0000-0000-0001-000000000005', NULL, 'Purple', 4, 'PublicAddress', 'Live public address — tone generator bypassed.', 0, 0, TRUE, 'Public Address'),
('00000000-0000-0000-0001-000000000006', NULL, 'Orange', 5, 'AirHorn', 'Air horn tone warning.', 0, 4, TRUE, 'Air Horn'),
('00000000-0000-0000-0001-000000000007', NULL, 'Yellow', 6, 'HiLo', 'Hi-Lo tone warning.', 0, 4, TRUE, 'Hi-Lo'),
('00000000-0000-0000-0001-000000000008', NULL, 'Yellow', 7, 'Whoop', 'Whoop tone warning.', 0, 4, TRUE, 'Whoop'),
('00000000-0000-0000-0001-000000000009', NULL, 'Green', 8, 'NoonTest', 'Short wail-2 tone (noon test).', 0, 4, TRUE, 'Noon Test'),
('00000000-0000-0000-0001-000000000010', NULL, 'Cyan', 15, 'SilentTest', 'Initiates diagnostic silent test, produces a status response.', 0, 4, TRUE, 'Silent Test'),
('00000000-0000-0000-0002-000000000001', NULL, 'Blue', 31, 'StatusRequest', 'Retrieves the full status byte from the siren.', 0, 4, TRUE, 'Status Request'),
('00000000-0000-0000-0002-000000000002', NULL, 'Green', 24, 'ArmSystem', 'Arms the Instant Status response.', 0, 4, TRUE, 'Arm System'),
('00000000-0000-0000-0002-000000000003', NULL, 'Red', 25, 'DisarmSystem', 'Disables the Instant Status response.', 0, 4, TRUE, 'Dis-arm System'),
('00000000-0000-0000-0002-000000000004', NULL, 'Green', 26, 'SirenOn', 'Enables the tone generator and digital voice.', 0, 4, TRUE, 'Siren On'),
('00000000-0000-0000-0002-000000000005', NULL, 'Red', 27, 'SirenOff', 'Disables the tone generator; digital voice stays active.', 0, 4, TRUE, 'Siren Off'),
('00000000-0000-0000-0002-000000000006', NULL, 'Cyan', 35, 'InstantStatus', 'Get real-time instant status of the remote siren station.', 0, 4, TRUE, 'Instant Status'),
('00000000-0000-0000-0002-000000000007', NULL, 'Blue', 22, 'Counter', 'Tone activation software counter request.', 0, 2, TRUE, 'Counter'),
('00000000-0000-0000-0002-000000000008', NULL, 'Blue', 23, 'ClearCounter', 'Clears the software tone activation counter to zero.', 0, 2, TRUE, 'Clear Counter'),
('00000000-0000-0000-0002-000000000009', NULL, 'Blue', 30, 'TestClear', 'Clears LEDs.', 0, 0, TRUE, 'Test Clear'),
('00000000-0000-0000-0002-000000000010', NULL, 'Green', 33, 'BatteryAC', 'Requests battery DC voltage and AC voltage measurements.', 0, 4, TRUE, 'Battery / AC'),
('00000000-0000-0000-0002-000000000011', NULL, 'Green', 34, 'BatteryTemp', 'Requests battery DC voltage and cabinet temperature.', 0, 4, TRUE, 'Battery / Temp'),
('00000000-0000-0000-0002-000000000012', NULL, 'Orange', 36, 'TransmitOff', 'Disables the transmit repeat feature during Instant Status.', 0, 0, TRUE, 'Transmit Off'),
('00000000-0000-0000-0003-000000000001', NULL, 'Purple', 17, 'Message13', 'Initiates digital voice message 13 (RSDVM module).', 0, 0, TRUE, 'Message 13'),
('00000000-0000-0000-0003-000000000002', NULL, 'Purple', 18, 'Message14', 'Initiates digital voice message 14 (RSDVM module).', 0, 0, TRUE, 'Message 14'),
('00000000-0000-0000-0003-000000000003', NULL, 'Purple', 19, 'Message15', 'Initiates digital voice message 15 (RSDVM module).', 0, 0, TRUE, 'Message 15'),
('00000000-0000-0000-0003-000000000004', NULL, 'Purple', 20, 'Message16', 'Initiates digital voice message 16 (RSDVM module).', 0, 0, TRUE, 'Message 16'),
('00000000-0000-0000-0004-000000000001', NULL, 'Purple', 49, 'Message1', 'Initiates digital voice message 1 (RSDVM module).', 0, 0, TRUE, 'Message 1'),
('00000000-0000-0000-0004-000000000002', NULL, 'Purple', 50, 'Message2', 'Initiates digital voice message 2 (RSDVM module).', 0, 0, TRUE, 'Message 2'),
('00000000-0000-0000-0004-000000000003', NULL, 'Purple', 51, 'Message3', 'Initiates digital voice message 3 (RSDVM module).', 0, 0, TRUE, 'Message 3'),
('00000000-0000-0000-0004-000000000004', NULL, 'Purple', 52, 'Message4', 'Initiates digital voice message 4 (RSDVM module).', 0, 0, TRUE, 'Message 4'),
('00000000-0000-0000-0004-000000000005', NULL, 'Purple', 53, 'Message5', 'Initiates digital voice message 5 (RSDVM module).', 0, 0, TRUE, 'Message 5'),
('00000000-0000-0000-0004-000000000006', NULL, 'Purple', 54, 'Message6', 'Initiates digital voice message 6 (RSDVM module).', 0, 0, TRUE, 'Message 6'),
('00000000-0000-0000-0004-000000000007', NULL, 'Purple', 55, 'Message7', 'Initiates digital voice message 7 (RSDVM module).', 0, 0, TRUE, 'Message 7'),
('00000000-0000-0000-0004-000000000008', NULL, 'Purple', 56, 'Message8', 'Initiates digital voice message 8 (RSDVM module).', 0, 0, TRUE, 'Message 8'),
('00000000-0000-0000-0004-000000000009', NULL, 'Purple', 59, 'Message9', 'Initiates digital voice message 9 (RSDVM module).', 0, 0, TRUE, 'Message 9'),
('00000000-0000-0000-0004-000000000010', NULL, 'Purple', 60, 'Message10', 'Initiates digital voice message 10 (RSDVM module).', 0, 0, TRUE, 'Message 10'),
('00000000-0000-0000-0004-000000000011', NULL, 'Purple', 61, 'Message11', 'Initiates digital voice message 11 (RSDVM module).', 0, 0, TRUE, 'Message 11'),
('00000000-0000-0000-0004-000000000012', NULL, 'Purple', 62, 'Message12', 'Initiates digital voice message 12 (RSDVM module).', 0, 0, TRUE, 'Message 12'),
('00000000-0000-0000-0005-000000000001', NULL, 'Yellow', 57, 'StrobeOn', 'Activates the strobe light.', 0, 0, TRUE, 'Strobe On'),
('00000000-0000-0000-0005-000000000002', NULL, 'Yellow', 58, 'StrobeOff', 'De-activates the strobe light.', 0, 0, TRUE, 'Strobe Off');

UPDATE `Users` SET `Created` = TIMESTAMP '2025-01-01 00:00:00'
WHERE `Id` = '00000000-0000-0000-0000-000000000001';
SELECT ROW_COUNT();


INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260520075536_AddCommandConfigFields', '9.0.0');

ALTER TABLE `CommandConfigs` ADD `IsSystemDefault` tinyint(1) NOT NULL DEFAULT FALSE;

UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0001-000000000001';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0001-000000000002';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0001-000000000003';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0001-000000000004';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0001-000000000005';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0001-000000000006';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0001-000000000007';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0001-000000000008';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0001-000000000009';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0001-000000000010';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0002-000000000001';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0002-000000000002';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0002-000000000003';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0002-000000000004';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0002-000000000005';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0002-000000000006';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0002-000000000007';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0002-000000000008';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0002-000000000009';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0002-000000000010';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0002-000000000011';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0002-000000000012';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0003-000000000001';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0003-000000000002';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0003-000000000003';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0003-000000000004';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0004-000000000001';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0004-000000000002';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0004-000000000003';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0004-000000000004';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0004-000000000005';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0004-000000000006';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0004-000000000007';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0004-000000000008';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0004-000000000009';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0004-000000000010';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0004-000000000011';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0004-000000000012';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0005-000000000001';
SELECT ROW_COUNT();


UPDATE `CommandConfigs` SET `IsSystemDefault` = TRUE
WHERE `Id` = '00000000-0000-0000-0005-000000000002';
SELECT ROW_COUNT();


INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260520080802_AddIsSystemDefault', '9.0.0');

COMMIT;

