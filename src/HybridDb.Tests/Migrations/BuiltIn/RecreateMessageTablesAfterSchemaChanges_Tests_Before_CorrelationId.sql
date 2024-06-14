SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[messages](
	[Topic] [nvarchar](850) NOT NULL,
	[Id] [nvarchar](850) NOT NULL,
	[CommitId] [uniqueidentifier] NOT NULL,
	[Discriminator] [nvarchar](850) NOT NULL,
	[Message] [nvarchar](max) NULL,
	[Version] [nvarchar](40) NOT NULL,
	[Metadata] [nvarchar](max) NOT NULL,
	[Position] [bigint] NOT NULL IDENTITY(0,1),
 CONSTRAINT [PK_messages] PRIMARY KEY CLUSTERED 
(
	[Topic] ASC,
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
INSERT [dbo].[messages] ([Topic], [Id], [CommitId], [Discriminator], [Message], [Version], [Metadata]) VALUES (N'errors', N'0360c82d-bc8c-4023-aa8c-1ca58b53e23a', N'22c3536a-2fad-4971-87e5-deb2d46a32b7', N'UpdateEstateReadModel', N'{"Id":"0360c82d-bc8c-4023-aa8c-1ca58b53e23a","CaseId":"xxx"}', N'0.0', N'{}')
GO
INSERT [dbo].[messages] ([Topic], [Id], [CommitId], [Discriminator], [Message], [Version], [Metadata]) VALUES (N'errors', N'29067f8d-b72f-411c-ad81-9fe618b4a316', N'5c689a90-e491-4b45-adfd-2a73c5c02d75', N'UpdateProposalGroupReadModels', N'{"Id":"29067f8d-b72f-411c-ad81-9fe618b4a316","CaseId":"yyy"}', N'0.0', N'{}')
GO

ALTER TABLE [dbo].[messages] ADD  DEFAULT ('0.0') FOR [Version]
GO
ALTER TABLE [dbo].[messages] ADD  DEFAULT ('{}') FOR [Metadata]
GO
