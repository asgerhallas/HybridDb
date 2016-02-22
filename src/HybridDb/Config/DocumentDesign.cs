using System;
using System.Collections.Generic;

namespace HybridDb.Config
{
    public class DocumentDesign
    {
        readonly Dictionary<string, DocumentDesign> decendentsAndSelf;

        public DocumentDesign(Configuration configuration, DocumentTable table, Type documentType, string discriminator)
        {
            DocumentType = documentType;
            Table = table;
            Discriminator = discriminator;
            
            decendentsAndSelf = new Dictionary<string, DocumentDesign>
            {
                { Discriminator, this }
            };
            
            Projections = new Dictionary<string, Projection>
            {
                {Table.DiscriminatorColumn, Projection.From<string>(_ => Discriminator)},
                {Table.DocumentColumn, Projection.From<byte[]>(managedEntity => configuration.Serializer.Serialize(managedEntity.Entity))},
                {
                    Table.MetadataColumn,
                    Projection.From<byte[]>(managedEntity =>
                        managedEntity.Metadata != null
                            ? configuration.Serializer.Serialize(managedEntity.Metadata)
                            : null)
                },
                {Table.VersionColumn, Projection.From<int>(_ => configuration.ConfiguredVersion)},
                {Table.AwaitsReprojectionColumn, Projection.From<bool>(_ => false)}
            };
        }

        public DocumentDesign(Configuration configuration, DocumentDesign parent, Type documentType, string discriminator)
            : this(configuration, parent.Table, documentType, discriminator)
        {
            Parent = parent;
            Projections = parent.Projections.ToDictionary();
            Projections[Table.DiscriminatorColumn] = Projection.From<string>(managedEntity => Discriminator);
        
            Parent.AddChild(this);
        }

        public DocumentDesign Parent { get; private set; }
        public Type DocumentType { get; private set; }
        public DocumentTable Table { get; private set; }
        public string Discriminator { get; private set; }

        public Dictionary<string, Projection> Projections { get; private set; }

        public IReadOnlyDictionary<string, DocumentDesign> DecendentsAndSelf => decendentsAndSelf;

        public Func<object, string> GetKey { get; internal set; } = entity => (string) (((dynamic) entity).Id ?? Guid.NewGuid().ToString());

        void AddChild(DocumentDesign design)
        {
            Parent?.AddChild(design);

            if (decendentsAndSelf.ContainsKey(design.Discriminator))
                throw new InvalidOperationException($"Discriminator '{design.Discriminator}' is already in use.");

            decendentsAndSelf.Add(design.Discriminator, design);
        }
    }

    public class Projection
    {
        Projection(Type returnType, Func<ManagedEntity, object> projector)
        {
            ReturnType = returnType;
            Projector = projector;
        }

        public static Projection From<TReturnType>(Func<ManagedEntity, object> projection)
        {
            return new Projection(typeof(TReturnType), projection);
        }

        public Type ReturnType { get; private set; }
        public Func<ManagedEntity, object> Projector { get; private set; }
    }
}