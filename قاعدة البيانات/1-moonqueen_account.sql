-- ============================================================================
-- MoonQueen Server (V6192) — Official Clean Account Database Schema
-- Server Brand : MoonQueen
-- Developer    : ahmedzahran (احمد زهران)
-- Target DB    : moonqueen_account (MariaDB 10.4+ / MySQL 8.0+ / XAMPP)
-- Safety Note  : Does NOT touch redmoon_account or redmoon_game.
--                Contains ZERO passwords, ZERO secret keys, ZERO test accounts.
-- ============================================================================

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!50503 SET NAMES utf8mb4 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;

CREATE DATABASE IF NOT EXISTS `moonqueen_account`
  DEFAULT CHARACTER SET utf8mb4
  COLLATE utf8mb4_general_ci;

USE `moonqueen_account`;

-- ----------------------------------------------------------------------------
-- 1. Table structure & complete role seed for `conquer_account_authority`
--    (Supports Player=1, Cooperator=2, Moderator..GM=7, PM=8, Admin/Owner=9..10
--     so FK_AuthorityId_AccountAuthority never blocks GM/PM account creation)
-- ----------------------------------------------------------------------------
DROP TABLE IF EXISTS `conquer_account_authority`;
CREATE TABLE `conquer_account_authority` (
  `Id` int(11) NOT NULL AUTO_INCREMENT,
  `Name` varchar(255) NOT NULL,
  `NormalizedName` varchar(255) NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=11 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

LOCK TABLES `conquer_account_authority` WRITE;
/*!40000 ALTER TABLE `conquer_account_authority` DISABLE KEYS */;
INSERT INTO `conquer_account_authority` (`Id`, `Name`, `NormalizedName`) VALUES
  (1, 'Player', 'PLAYER'),
  (2, 'Cooperator', 'COOPERATOR'),
  (3, 'Helper', 'HELPER'),
  (4, 'Moderator', 'MODERATOR'),
  (5, 'Supervisor', 'SUPERVISOR'),
  (6, 'EventManager', 'EVENTMANAGER'),
  (7, 'GM', 'GM'),
  (8, 'PM', 'PM'),
  (9, 'Administrator', 'ADMINISTRATOR'),
  (10, 'Owner', 'OWNER');
/*!40000 ALTER TABLE `conquer_account_authority` ENABLE KEYS */;
UNLOCK TABLES;

-- ----------------------------------------------------------------------------
-- 2. Table structure for `conquer_account` (Clean — AUTO_INCREMENT=1)
--    Compatible 100% with MoonQueen.Login, موقع السيرفر/, لوحة تحكم السيرفر/,
--    and تشغيل السيرفر/.
-- ----------------------------------------------------------------------------
DROP TABLE IF EXISTS `conquer_account`;
CREATE TABLE `conquer_account` (
  `Id` int(11) NOT NULL AUTO_INCREMENT,
  `UserName` varchar(255) NOT NULL,
  `Password` varchar(255) NOT NULL,
  `Salt` varchar(255) NOT NULL,
  `AuthorityId` int(11) NOT NULL DEFAULT 1,
  `Flag` int(11) NOT NULL DEFAULT 1,
  `IpAddress` varchar(255) DEFAULT '127.0.0.1',
  `MacAddress` varchar(255) DEFAULT '000000000000',
  `ParentId` char(36) NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
  `Created` datetime NOT NULL DEFAULT current_timestamp(),
  `Modified` datetime DEFAULT NULL,
  `Deleted` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `UX_ConquerAccount_UserName` (`UserName`),
  KEY `FK_AuthorityId_AccountAuthority` (`AuthorityId`),
  CONSTRAINT `FK_AuthorityId_AccountAuthority` FOREIGN KEY (`AuthorityId`) REFERENCES `conquer_account_authority` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=1 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- ----------------------------------------------------------------------------
-- 3. Table structure for `conquer_account_vip` (Clean — AUTO_INCREMENT=1)
--    ConquerAccountId is NOT NULL DEFAULT 0 to match C# non-nullable int.
-- ----------------------------------------------------------------------------
DROP TABLE IF EXISTS `conquer_account_vip`;
CREATE TABLE `conquer_account_vip` (
  `Id` int(11) NOT NULL AUTO_INCREMENT,
  `GameAccountId` int(11) NOT NULL,
  `VipLevel` tinyint(4) NOT NULL DEFAULT 1,
  `DurationMinutes` int(10) unsigned NOT NULL DEFAULT 0,
  `StartDate` datetime NOT NULL DEFAULT current_timestamp(),
  `EndDate` datetime NOT NULL DEFAULT current_timestamp(),
  `CreationDate` datetime NOT NULL DEFAULT current_timestamp(),
  `ConquerAccountId` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`Id`),
  KEY `FK_GameAccountVip_GameAccount` (`GameAccountId`),
  CONSTRAINT `FK_GameAccountVip_GameAccount` FOREIGN KEY (`GameAccountId`) REFERENCES `conquer_account` (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=1 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- ----------------------------------------------------------------------------
-- 4. Table structure for `realm` (Clean — ready for local RPC setup)
--    Safe NOT NULL DEFAULT '' on legacy Database* columns prevents STRICT_TRANS_TABLES
--    ERROR 1364 on MariaDB 10.4.32 when inserting realm config rows.
-- ----------------------------------------------------------------------------
DROP TABLE IF EXISTS `realm`;
CREATE TABLE `realm` (
  `RealmID` char(36) NOT NULL,
  `Name` varchar(255) NOT NULL,
  `AuthorityID` smallint(5) unsigned NOT NULL DEFAULT 1,
  `GameIPAddress` varchar(255) NOT NULL DEFAULT '127.0.0.1',
  `RpcIPAddress` varchar(255) NOT NULL DEFAULT '127.0.0.1',
  `GamePort` int(10) unsigned NOT NULL DEFAULT 5816,
  `RpcPort` int(10) unsigned NOT NULL DEFAULT 9865,
  `Status` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `Username` varchar(255) NOT NULL,
  `Password` varchar(255) NOT NULL,
  `LastPing` datetime DEFAULT NULL,
  `DatabaseHost` varchar(255) NOT NULL DEFAULT '',
  `DatabaseUser` varchar(255) NOT NULL DEFAULT '',
  `DatabasePass` varchar(255) NOT NULL DEFAULT '',
  `DatabaseSchema` varchar(255) NOT NULL DEFAULT 'moonqueen_game',
  `DatabasePort` varchar(255) NOT NULL DEFAULT '3306',
  `Active` tinyint(1) NOT NULL DEFAULT 1,
  `ProductionRealm` tinyint(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (`RealmID`),
  UNIQUE KEY `UX_Realm_Name` (`Name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- ----------------------------------------------------------------------------
-- 5. Table structure for `realms_status` (DbRealmStatus compatibility)
-- ----------------------------------------------------------------------------
DROP TABLE IF EXISTS `realms_status`;
CREATE TABLE `realms_status` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `realm_id` int(10) unsigned NOT NULL DEFAULT 0,
  `realm_name` varchar(255) NOT NULL DEFAULT 'MoonQueen',
  `old_status` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `new_status` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `time` datetime NOT NULL DEFAULT current_timestamp(),
  `players_online` int(10) unsigned NOT NULL DEFAULT 0,
  `max_players_online` int(10) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=1 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;
/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;
