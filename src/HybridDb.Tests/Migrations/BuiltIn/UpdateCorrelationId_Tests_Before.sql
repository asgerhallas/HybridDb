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
    [CorrelationId] [nvarchar](850) NOT NULL default 'N/A',
 CONSTRAINT [PK_messages] PRIMARY KEY CLUSTERED 
(
	[Topic] ASC,
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
INSERT [dbo].[messages] ([Topic], [Id], [CommitId], [Discriminator], [Message], [Version], [Metadata]) VALUES (N'dummy', N'0360c82d-bc8c-4023-aa8c-1ca58b53e23a', N'22c3536a-2fad-4971-87e5-deb2d46a32b7', N'UpdateEstateReadModel', N'{"Id":"0360c82d-bc8c-4023-aa8c-1ca58b53e23a","CaseId":"xxx"}', N'0.0', N'{"correlation-ids": "[\"id1\",\"id2\"]"}')
GO
INSERT [dbo].[messages] ([Topic], [Id], [CommitId], [Discriminator], [Message], [Version], [Metadata]) VALUES (N'dummy', N'29067f8d-b72f-411c-ad81-9fe618b4a316', N'5c689a90-e491-4b45-adfd-2a73c5c02d75', N'UpdateProposalGroupReadModels', N'{"Id":"29067f8d-b72f-411c-ad81-9fe618b4a316","CaseId":"yyy"}', N'0.0', N'{"correlation-ids": "[\"id3\", \"id4\"]"}')
GO
INSERT [dbo].[messages] ([Topic], [Id], [CommitId], [Discriminator], [Message], [Version], [Metadata]) VALUES (N'dummy', N'63467c49-0457-4090-a40e-b002b115c3f9', N'55a20b4c-72e2-4118-b123-7feb2299dd88', N'Dummy', N'{"Id":"71102e4f-b925-439d-8fba-fa82b1eab891","CaseId":"zzz"}', N'0.0', N'{}')
GO

ALTER TABLE [dbo].[messages] ADD  DEFAULT ('0.0') FOR [Version]
GO
ALTER TABLE [dbo].[messages] ADD  DEFAULT ('{}') FOR [Metadata]
GO
