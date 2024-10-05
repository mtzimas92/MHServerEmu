-- Initialize a new database file using the current schema version

CREATE TABLE  Account  (
	 Id 	BIGINT NOT NULL UNIQUE,
	 Email 	VARCHAR(50) NOT NULL UNIQUE,
	 PlayerName 	VARCHAR(50) NOT NULL UNIQUE,
	 PasswordHash 	BLOB(255) NOT NULL,
	 Salt 	BLOB(50) NOT NULL,
	 UserLevel 	INTEGER NOT NULL,
	 Flags 	INTEGER NOT NULL,
	PRIMARY KEY( Id )
);

CREATE TABLE  Player  (
	 DbGuid 	BIGINT NOT NULL UNIQUE,
	 ArchiveData 	BLOB(1000),
	 StartTarget 	BIGINT,
	 StartTargetRegionOverride 	BIGINT,
	 AOIVolume 	INTEGER,
	FOREIGN KEY( DbGuid ) REFERENCES  Account ( Id ) ON DELETE CASCADE,
	PRIMARY KEY( DbGuid )
);

CREATE TABLE  Avatar  (
	 DbGuid 	BIGINT NOT NULL UNIQUE,
	 ContainerDbGuid 	BIGINT,
	 InventoryProtoGuid 	BIGINT,
	 Slot 	INTEGER,
	 EntityProtoGuid 	BIGINT,
	 ArchiveData 	BLOB(1000),
	FOREIGN KEY( ContainerDbGuid ) REFERENCES  Player ( DbGuid ) ON DELETE CASCADE,
	PRIMARY KEY( DbGuid )
);

CREATE TABLE  TeamUp  (
	 DbGuid 	BIGINT NOT NULL UNIQUE,
	 ContainerDbGuid 	BIGINT,
	 InventoryProtoGuid 	BIGINT,
	 Slot 	INTEGER,
	 EntityProtoGuid 	BIGINT,
	 ArchiveData 	BLOB(1000),
	FOREIGN KEY( ContainerDbGuid ) REFERENCES  Player ( DbGuid ) ON DELETE CASCADE,
	PRIMARY KEY( DbGuid )
);

CREATE TABLE  Item  (
	 DbGuid 	BIGINT NOT NULL UNIQUE,
	 ContainerDbGuid 	BIGINT,
	 InventoryProtoGuid 	BIGINT,
	 Slot 	INTEGER,
	 EntityProtoGuid 	BIGINT,
	 ArchiveData 	BLOB(1000),
	FOREIGN KEY( ContainerDbGuid ) REFERENCES  Player ( DbGuid ) ON DELETE CASCADE,
	FOREIGN KEY( ContainerDbGuid ) REFERENCES  Avatar ( DbGuid ) ON DELETE CASCADE,
	FOREIGN KEY( ContainerDbGuid ) REFERENCES  TeamUp ( DbGuid ) ON DELETE CASCADE,
	PRIMARY KEY( DbGuid )
);

CREATE TABLE  ControlledEntity  (
	 DbGuid 	BIGINT NOT NULL UNIQUE,
	 ContainerDbGuid 	BIGINT,
	 InventoryProtoGuid 	BIGINT,
	 Slot 	INTEGER,
	 EntityProtoGuid 	BIGINT,
	 ArchiveData 	BLOB(1000),
	FOREIGN KEY( ContainerDbGuid ) REFERENCES  Avatar ( DbGuid ) ON DELETE CASCADE,
	PRIMARY KEY( DbGuid )
);

CREATE TABLE  PRAGMA  (
	 user_version 	INT NOT NULL UNIQUE
);

INSERT INTO pragma (user_version) VALUES (2);

CREATE INDEX  IX_Avatar_ContainerDbGuid  ON  Avatar  ( ContainerDbGuid );
CREATE INDEX  IX_TeamUp_ContainerDbGuid  ON  TeamUp  ( ContainerDbGuid );
CREATE INDEX  IX_Item_ContainerDbGuid  ON  Item  ( ContainerDbGuid );
CREATE INDEX  IX_ControlledEntity_ContainerDbGuid  ON  ControlledEntity  ( ContainerDbGuid );
