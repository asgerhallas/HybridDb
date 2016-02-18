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
                {Table.IdColumn, Projection.From<Guid>(document => ((dynamic) document).Id)},
                {Table.DiscriminatorColumn, Projection.From<string>(document => Discriminator)},
                {Table.DocumentColumn, Projection.From<byte[]>(document => configuration.Serializer.Serialize(document))},
                {Table.VersionColumn, Projection.From<int>(document => configuration.ConfiguredVersion)},
                {Table.AwaitsReprojectionColumn, Projection.From<bool>(document => false)}
            };
        }

        public DocumentDesign(Configuration configuration, DocumentDesign parent, Type documentType, string discriminator)
            : this(configuration, parent.Table, documentType, discriminator)
        {
            Parent = parent;
            Projections = parent.Projections.ToDictionary();
            Projections[Table.DiscriminatorColumn] = Projection.From<string>(document => Discriminator);
        
            Parent.AddChild(this);
        }

        public DocumentDesign Parent { get; private set; }
        public Type DocumentType { get; private set; }
        public DocumentTable Table { get; private set; }
        public string Discriminator { get; private set; }

        public Dictionary<string, Projection> Projections { get; private set; }

        public IReadOnlyDictionary<string, DocumentDesign> DecendentsAndSelf
        {
            get { return decendentsAndSelf; }
        }

        public string GetKey(object entity)
        {
            return (string)(Projections[Table.IdColumn].Projector(entity) ?? Guid.NewGuid().ToString());
        }

        void AddChild(DocumentDesign design)
        {
            if (Parent != null)
            {
                Parent.AddChild(design);
            }

            if (decendentsAndSelf.ContainsKey(design.Discriminator))
                throw new InvalidOperationException(string.Format("Discriminator '{0}' is already in use.", design.Discriminator));

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

        //public static Projection From<TEntity, TReturnType>(Func<TEntity, TReturnType> projection)
        //{
        //    return new Projection(typeof(TEntity), typeof(TReturnType), projection);
        //}

        //public Type DeclaringType { get; private set; }
        public Type ReturnType { get; private set; }
        public Func<object, object> Projector { get; private set; }
    }
}