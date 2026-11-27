-- v2.6: direct chat between accounts (CHAT_CONVERSATION + CHAT_MESSAGE). Idempotent; the API also applies this at startup.
-- The (user_low_id, user_high_id) pair is stored ordered by the API (low < high by GUID comparison)
-- so one conversation exists per pair of accounts.
IF OBJECT_ID(N'dbo.CHAT_CONVERSATION', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CHAT_CONVERSATION (
        conversation_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CHAT_CONVERSATION_conversation_id DEFAULT NEWSEQUENTIALID(),
        user_low_id UNIQUEIDENTIFIER NOT NULL,
        user_high_id UNIQUEIDENTIFIER NOT NULL,
        created_at DATETIME2(0) NOT NULL CONSTRAINT DF_CHAT_CONVERSATION_created_at DEFAULT SYSUTCDATETIME(),
        last_message_at DATETIME2(0) NULL,
        CONSTRAINT PK_CHAT_CONVERSATION PRIMARY KEY (conversation_id),
        CONSTRAINT UQ_CHAT_CONVERSATION_pair UNIQUE (user_low_id, user_high_id)
    );
END;
IF OBJECT_ID(N'dbo.CHAT_MESSAGE', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CHAT_MESSAGE (
        message_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CHAT_MESSAGE_message_id DEFAULT NEWSEQUENTIALID(),
        conversation_id UNIQUEIDENTIFIER NOT NULL,
        sender_id UNIQUEIDENTIFIER NOT NULL,
        body NVARCHAR(2000) NOT NULL,
        sent_at DATETIME2(0) NOT NULL CONSTRAINT DF_CHAT_MESSAGE_sent_at DEFAULT SYSUTCDATETIME(),
        read_at DATETIME2(0) NULL,
        CONSTRAINT PK_CHAT_MESSAGE PRIMARY KEY (message_id)
    );
END;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CHAT_CONVERSATION_user_low_id')
    ALTER TABLE dbo.CHAT_CONVERSATION ADD CONSTRAINT FK_CHAT_CONVERSATION_user_low_id FOREIGN KEY (user_low_id) REFERENCES dbo.USER_ACCOUNT(user_id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CHAT_CONVERSATION_user_high_id')
    ALTER TABLE dbo.CHAT_CONVERSATION ADD CONSTRAINT FK_CHAT_CONVERSATION_user_high_id FOREIGN KEY (user_high_id) REFERENCES dbo.USER_ACCOUNT(user_id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CHAT_MESSAGE_conversation_id')
    ALTER TABLE dbo.CHAT_MESSAGE ADD CONSTRAINT FK_CHAT_MESSAGE_conversation_id FOREIGN KEY (conversation_id) REFERENCES dbo.CHAT_CONVERSATION(conversation_id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CHAT_MESSAGE_sender_id')
    ALTER TABLE dbo.CHAT_MESSAGE ADD CONSTRAINT FK_CHAT_MESSAGE_sender_id FOREIGN KEY (sender_id) REFERENCES dbo.USER_ACCOUNT(user_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CHAT_CONVERSATION_user_low_id' AND object_id = OBJECT_ID(N'dbo.CHAT_CONVERSATION'))
    CREATE INDEX IX_CHAT_CONVERSATION_user_low_id ON dbo.CHAT_CONVERSATION(user_low_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CHAT_CONVERSATION_user_high_id' AND object_id = OBJECT_ID(N'dbo.CHAT_CONVERSATION'))
    CREATE INDEX IX_CHAT_CONVERSATION_user_high_id ON dbo.CHAT_CONVERSATION(user_high_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CHAT_MESSAGE_conversation_id' AND object_id = OBJECT_ID(N'dbo.CHAT_MESSAGE'))
    CREATE INDEX IX_CHAT_MESSAGE_conversation_id ON dbo.CHAT_MESSAGE(conversation_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CHAT_MESSAGE_sender_id' AND object_id = OBJECT_ID(N'dbo.CHAT_MESSAGE'))
    CREATE INDEX IX_CHAT_MESSAGE_sender_id ON dbo.CHAT_MESSAGE(sender_id);
