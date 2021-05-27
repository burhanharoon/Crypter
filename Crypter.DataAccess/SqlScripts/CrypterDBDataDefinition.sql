﻿SET FOREIGN_KEY_CHECKS = 0;
DROP TABLE IF EXISTS `Users`;
DROP TABLE IF EXISTS `Messages`;
DROP TABLE IF EXISTS `Files`;
DROP TABLE IF EXISTS `Keys`;
DROP TABLE IF EXISTS `BetaKeys`;
SET FOREIGN_KEY_CHECKS = 1;

CREATE TABLE `Users` (
  `Id` VARCHAR(36) NOT NULL,
  `UserName` VARCHAR(64) UNIQUE NOT NULL,
  `Email` VARCHAR(64),
  `PublicAlias` VARCHAR(64),
  `PasswordHash` VARBINARY(256) NOT NULL,
  `PasswordSalt` VARBINARY(256) NOT NULL,
  `IsPublic` TINYINT NOT NULL,
  `AllowAnonymousMessages` TINYINT NOT NULL,
  `AllowAnonymousFiles` TINYINT NOT NULL,
  `Created` TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
  PRIMARY KEY (Id)
 ) ENGINE=InnoDB;

CREATE TABLE `Messages` (
  `Id` VARCHAR(36) NOT NULL,
  `Recipient` VARCHAR(36),
  `Sender` VARCHAR(36) NOT NULL,
  `Subject` VARCHAR(256),
  `Size` INT NOT NULL,
  `CipherTextPath` VARCHAR(256) NOT NULL,
  `Signature` VARCHAR(256) NOT NULL,
  `SymmetricInfo` TEXT NOT NULL,
  `PublicKey` TEXT NOT NULL,
  `ServerIV` VARBINARY(256),
  `ServerDigest` VARBINARY(256),
  `Created` TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
  `Expiration` TIMESTAMP NOT NULL,
  PRIMARY KEY (Id)
) ENGINE=InnoDB;

CREATE TABLE `Files` (
  `Id` VARCHAR(36) NOT NULL,
  `Recipient` VARCHAR(36), 
  `Sender` VARCHAR(36),
  `FileName` VARCHAR(256) NOT NULL,
  `ContentType` VARCHAR(256) NOT NULL,
  `Size` INT NOT NULL,
  `CipherTextPath` VARCHAR(256) NOT NULL,
  `Signature` VARCHAR(256) NOT NULL,
  `SymmetricInfo` TEXT NOT NULL,
  `PublicKey` TEXT NOT NULL,
  `ServerIV` VARBINARY(256),
  `ServerDigest` VARBINARY(256),
  `Created` TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
  `Expiration` TIMESTAMP NOT NULL,
  PRIMARY KEY (Id)
) ENGINE=InnoDB;

CREATE TABLE `Keys` (
  `Id` VARCHAR(36) NOT NULL,
  `Owner` VARCHAR(36) NOT NULL,
  `PrivateKey` TEXT NOT NULL,
  `PublicKey` TEXT NOT NULL,
  `KeyType` TINYINT NOT NULL,
  `Created` TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
  PRIMARY KEY(Id),
  FOREIGN KEY(Owner) REFERENCES Users(Id)
) ENGINE=InnoDB;

CREATE TABLE `BetaKeys` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `Key` VARCHAR(32) NOT NULL UNIQUE,
  PRIMARY KEY(Id)
) ENGINE=InnoDB;