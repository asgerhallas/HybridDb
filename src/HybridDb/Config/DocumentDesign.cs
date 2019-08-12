using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace HybridDb.Config
{
    public class DocumentDesign
    {
        readonly Dictionary<string, DocumentDesign> decendentsAndSelf;

        public DocumentDesign(Configuration configuration, DocumentTable table, Type documentType, string discriminator)
        {
            Root = this;
            DocumentType = documentType;
            Table = table;
            Discriminator = discriminator;

            if (DocumentTable.DiscriminatorColumn.Length > -1 && discriminator.Length > DocumentTable.DiscriminatorColumn.Length)
            {
                throw new InvalidOperationException($"Discriminator '{discriminator}' is too long for column. Maximum length is {DocumentTable.DiscriminatorColumn.Length}.");
            }

            decendentsAndSelf = new Dictionary<string, DocumentDesign> {{Discriminator, this}};

            GetKey = configuration.DefaultKeyResolver;

            Projections = new Dictionary<string, Projection>
            {
                [DocumentTable.DiscriminatorColumn] = Projection.From<string>(_ => Discriminator),
                [DocumentTable.DocumentColumn] = Projection.From<byte[]>(document => configuration.Serializer.Serialize(document)),
                [DocumentTable.MetadataColumn] = Projection.From<byte[]>((document, metadata) =>
                    metadata != null
                        ? configuration.Serializer.Serialize(metadata)
                        : null),
                [DocumentTable.VersionColumn] = Projection.From<int>(_ => configuration.ConfiguredVersion),
                [DocumentTable.AwaitsReprojectionColumn] = Projection.From<bool>(_ => false)
            };
        }

        public DocumentDesign(Configuration configuration, DocumentDesign parent, Type documentType, string discriminator)
            : this(configuration, parent.Table, documentType, discriminator)
        {
            Root = parent.Root;
            Parent = parent;
            Projections = parent.Projections.ToDictionary();
            Projections[DocumentTable.DiscriminatorColumn] = Projection.From<string>(_ => Discriminator);
        
            Parent.AddChild(this);
        }

        public DocumentDesign Root { get; }
        public DocumentDesign Parent { get; }
        public Type DocumentType { get; }
        public DocumentTable Table { get; }
        public string Discriminator { get; }

        public Dictionary<string, Projection> Projections { get; }

        public IReadOnlyDictionary<string, DocumentDesign> DecendentsAndSelf => decendentsAndSelf;

        public Func<object, string> GetKey { get; internal set; } 

        void AddChild(DocumentDesign design)
        {
            Parent?.AddChild(design);

            if (decendentsAndSelf.ContainsKey(design.Discriminator))
            {
                throw new InvalidOperationException($"Discriminator '{design.Discriminator}' is already in use.");
            }

            decendentsAndSelf.Add(design.Discriminator, design);
        }
    }
}