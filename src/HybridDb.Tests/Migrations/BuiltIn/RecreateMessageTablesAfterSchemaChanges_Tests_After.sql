SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[messages](
	[Version] [nvarchar](40) NOT NULL,
	[Topic] [nvarchar](850) NOT NULL,
	[Position] [bigint] IDENTITY(0,1) NOT NULL,
	[Discriminator] [nvarchar](850) NOT NULL,
	[Id] [nvarchar](850) NOT NULL,
	[Order] [int] NOT NULL,
	[CommitId] [uniqueidentifier] NOT NULL,
	[Message] [nvarchar](max) NULL,
	[Metadata] [nvarchar](max) NULL,
	[CorrelationId] [nvarchar](850) NOT NULL,
 CONSTRAINT [PK_messages] PRIMARY KEY CLUSTERED 
(
	[Topic] ASC,
	[Order] ASC,
	[Position] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF)
)

GO
SET IDENTITY_INSERT [dbo].[messages] ON 

GO
INSERT [dbo].[messages] ([Version], [Topic], [Position], [Discriminator], [Id], [Order], [CommitId], [Message], [Metadata], [CorrelationId]) VALUES (N'0.0', N'errors', 0, N'UpdateEstateReadModel', N'0360c82d-bc8c-4023-aa8c-1ca58b53e23a', 1, N'22c3536a-2fad-4971-87e5-deb2d46a32b7', N'{"Id":"0360c82d-bc8c-4023-aa8c-1ca58b53e23a","CaseId":"xxx"}', N'{}', N'N/A')
GO
INSERT [dbo].[messages] ([Version], [Topic], [Position], [Discriminator], [Id], [Order], [CommitId], [Message], [Metadata], [CorrelationId]) VALUES (N'0.0', N'errors', 1, N'UpdateProposalGroupReadModels', N'29067f8d-b72f-411c-ad81-9fe618b4a316', 1, N'5c689a90-e491-4b45-adfd-2a73c5c02d75', N'{"Id":"29067f8d-b72f-411c-ad81-9fe618b4a316","CaseId":"yyy"}', N'{}', N'N/A')
GO
SET IDENTITY_INSERT [dbo].[messages] OFF
GO
SET ANSI_PADDING ON

GO
CREATE UNIQUE NONCLUSTERED INDEX [messages_Topic_Id] ON [dbo].[messages]
(
	[Topic] ASC,
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF)
GO
ALTER TABLE [dbo].[messages] ADD  DEFAULT ('0.0') FOR [Version]
GO
ALTER TABLE [dbo].[messages] ADD  DEFAULT ((1)) FOR [Order]
GO
ALTER TABLE [dbo].[messages] ADD  DEFAULT ('{}') FOR [Metadata]
GO
ALTER TABLE [dbo].[messages] ADD  DEFAULT ('N/A') FOR [CorrelationId]