-- Delete old placeholder tables while keeping existing account records
DROP TABLE Avatar;
DROP TABLE Player;

-- Initialize new tables for storing persistent entities
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
-- Create indexes for faster container lookup queries
CREATE INDEX  IX_Avatar_ContainerDbGuid  ON  Avatar  ( ContainerDbGuid );
CREATE INDEX  IX_TeamUp_ContainerDbGuid  ON  TeamUp  ( ContainerDbGuid );
CREATE INDEX  IX_Item_ContainerDbGuid  ON  Item  ( ContainerDbGuid );
CREATE INDEX  IX_ControlledEntity_ContainerDbGuid  ON  ControlledEntity  ( ContainerDbGuid );