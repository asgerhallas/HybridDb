CREATE TABLE [dbo].[Entities] (
    [Id] [nvarchar](850) NOT NULL,
    [Etag] [uniqueidentifier] NOT NULL,
    [CreatedAt] [datetimeoffset](7) NOT NULL,
    [ModifiedAt] [datetimeoffset](7) NOT NULL,
    [Document] [nvarchar](max) NOT NULL,
    [Metadata] [nvarchar](max) NOT NULL,
    [Discriminator] [nvarchar](850) NOT NULL,
    [AwaitsReprojection] [bit] NOT NULL,
    [Version] [int] NOT NULL,
    [Timestamp] [rowversion] NOT NULL,
    [LastOperation] [tinyint] NOT NULL,
    [Property] [nvarchar](max) NOT NULL,
    CONSTRAINT [PK_Entities] PRIMARY KEY CLUSTERED ([Id] ASC)
)
GO

INSERT INTO [dbo].[Entities]
    ([Id], [Etag], [CreatedAt], [ModifiedAt], [Document], [Metadata], [Discriminator], [AwaitsReprojection], [Version], [LastOperation], [Property])
VALUES
    ('active-id', '11111111-1111-1111-1111-111111111111', SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), '{}', '{}', 'Entity', 0, 0, 1, 'active'),
    ('deleted-id/00000000-0000-0000-0000-000000000001', '22222222-2222-2222-2222-222222222222', SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), '{}', '{}', 'Entity', 0, 0, 4, 'deleted');
