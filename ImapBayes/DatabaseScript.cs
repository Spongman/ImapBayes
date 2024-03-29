﻿using System;
using System.Collections.Generic;
using System.Text;

namespace ImapBayes
{
	public class DatabaseScript
	{
		public const string Script = @"
CREATE TABLE tblAccounts (
	id INT IDENTITY PRIMARY KEY,
	strName NVARCHAR(100) NOT NULL,
	strHost VARCHAR(100) NOT NULL,
	strUser VARCHAR(100) NOT NULL,
	strPass VARCHAR(100) NOT NULL,
	nPort INT NOT NULL,
	fUseSsl BIT NOT NULL,
	fActive BIT NOT NULL,
	fTraining BIT NOT NULL,
	strInbox VARCHAR(100) NOT NULL,
	strSpam VARCHAR(100) NOT NULL,
	strUnsure VARCHAR(100) NOT NULL,
	cSpam INT NOT NULL,
	cHam INT NOT NULL,
	nSpamCutoff REAL NOT NULL,
	nHamCutoff REAL NOT NULL
)

/*
CREATE TABLE tblTrainingFolders (
	id INT IDENTITY PRIMARY KEY,
	idAccount INT NOT NULL,
	strFolder VARCHAR(100) NOT NULL,
	fSpam BIT NULL,
	fRecursive BIT NOT NULL
)
*/

CREATE TABLE tblMessages (
	id INT IDENTITY PRIMARY KEY,
	idAccount INT NOT NULL,
	strId VARCHAR(200),
	strSubject NVARCHAR(200),
	fSpam BIT NULL,
	fTrained BIT NOT NULL,
	nScore REAL NULL
)

CREATE INDEX IX_idAccountstrId
ON tblMessages (idAccount, strId)

CREATE TABLE tblTokens (
	id INT IDENTITY PRIMARY KEY,
	value nvarchar(100) NOT NULL
)

CREATE UNIQUE INDEX ixTokensValue
ON tblTokens (value ASC)


/*
CREATE TABLE tblMessageTokens (
	idMessage INT NOT NULL,
	idToken INT NOT NULL,
	--cOccurrences INT NOT NULL,
	CONSTRAINT PK_tblMessageTokens PRIMARY KEY CLUSTERED (idMessage, idToken)
) WITHOUT ROWID
*/


CREATE TABLE tblTokenCounts (
	idAccount int NOT NULL,
	idToken int NOT NULL,
	cSpam int NOT NULL,
	cHam int NOT NULL,
	CONSTRAINT PK_tblTokenCounts PRIMARY KEY CLUSTERED (idAccount, idToken)
) WITHOUT ROWID

";
	}
}
