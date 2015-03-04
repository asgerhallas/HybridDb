using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace HybridDb.Schema
{
    public class DocumentDesign
    {
        readonly Dictionary<string, DocumentDesign> decendentsAndSelf;

        public DocumentDesign(Configuration configuration, DocumentTable table, Type documentType)
        {
            Configuration = configuration;
            DocumentType = documentType;
            Table = table;
            Discriminator = documentType.Name;
            
            decendentsAndSelf = new Dictionary<string, DocumentDesign>
            {
                { Discriminator, this }
            };
            
            Projections = new Dictionary<string, Projection>
            {
                {Table.IdColumn, Projection.From<Guid>(document => ((dynamic) document).Id)},
                {Table.DiscriminatorColumn, Projection.From<string>(document => Discriminator)},
                {Table.DocumentColumn, Projection.From<byte[]>(document => configuration.Serializer.Serialize(document))}
            };
        }

        public DocumentDesign(Configuration configuration, DocumentDesign parent, Type documentType)
            : this(configuration, parent.Table, documentType)
        {
            Parent = parent;
            Projections = parent.Projections.ToDictionary();
            Projections[Table.DiscriminatorColumn] = Projection.From<string>(document => Discriminator);
        
            Parent.AddChild(this);
        }

        public Configuration Configuration { get; private set; }
        public DocumentDesign Parent { get; private set; }
        public Type DocumentType { get; private set; }
        public DocumentTable Table { get; private set; }
        public string Discriminator { get; private set; }

        public Dictionary<string, Projection> Projections { get; private set; }

        public IReadOnlyDictionary<string, DocumentDesign> DecendentsAndSelf
        {
            get { return decendentsAndSelf; }
        }

        public Guid GetId(object entity)
        {
            return (Guid) Projections[Table.IdColumn].Projector(entity);
        }


        void AddChild(DocumentDesign design)
        {
            if (Parent != null)
            {
                Parent.AddChild(design);
            }

            decendentsAndSelf.Add(design.Discriminator, design);
        }
    }

    public class Projection
    {
        Projection(Type returnType, Func<object, object> projector)
        {
            ReturnType = returnType;
            Projector = projector;
        }

        public static Projection From<TReturnType>(Func<object, object> projection)
        {
            return new Projection(typeof(TReturnType), projection);
        }
            
        public Type ReturnType { get; private set; }
        public Func<object, object> Projector { get; private set; }
    }
}