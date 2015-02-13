using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Linq;

namespace HybridDb.Schema
{
    public class DocumentDesign
    {
        readonly Dictionary<string, DocumentDesign> decendentsAndSelf;

        public DocumentDesign(Configuration configuration, DocumentTable table, Type type)
        {
            Configuration = configuration;
            Type = type;
            Table = table;
            Discriminator = type.Name;
            
            decendentsAndSelf = new Dictionary<string, DocumentDesign>
            {
                { Discriminator, this }
            };
            
            Projections = new Dictionary<string, Func<object, object>>
            {
                {Table.IdColumn, document => ((dynamic) document).Id},
                {Table.DiscriminatorColumn, document => Discriminator},
                {Table.DocumentColumn, document => configuration.Serializer.Serialize(document)}
            };
        }

        public DocumentDesign(Configuration configuration, DocumentDesign parent, Type type)
            : this(configuration, parent.Table, type)
        {
            Parent = parent;
            Projections = parent.Projections.ToDictionary();
            Projections[Table.DiscriminatorColumn] = document => Discriminator;
        
            Parent.AddChild(this);
        }

        public Configuration Configuration { get; private set; }
        public DocumentDesign Parent { get; private set; }
        public Type Type { get; private set; }
        public DocumentTable Table { get; private set; }
        public string Discriminator { get; private set; }

        public Dictionary<string, Func<object, object>> Projections { get; private set; }

        public IReadOnlyDictionary<string, DocumentDesign> DecendentsAndSelf
        {
            get { return decendentsAndSelf; }
        }

        public Guid GetId(object entity)
        {
            return (Guid) Projections[Table.IdColumn](entity);
        }

        protected Func<object, object> Compile<TEntity, TMember>(string name, Expression<Func<TEntity, TMember>> projector)
        {
            var compiled = projector.Compile();
            return entity =>
            {
                try
                {
                    return (object) compiled((TEntity) entity);
                }
                catch (Exception ex)
                {
                    throw new TargetInvocationException(
                        string.Format("The projector for column {0} threw an exception.\nThe projector code is {1}.", name, projector), ex);
                }
            };
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

    public class DocumentDesign<TEntity> : DocumentDesign
    {
        public DocumentDesign(Configuration configuration, DocumentDesign design) : base(configuration, design, typeof (TEntity)) {}
        public DocumentDesign(Configuration configuration, DocumentTable table) : base(configuration, table, typeof (TEntity)) {}

        public DocumentDesign<TEntity> With<TMember>(Expression<Func<TEntity, TMember>> projector, bool makeNullSafe = true)
        {
            var name = Configuration.GetColumnNameByConventionFor(projector);
            return With(name, projector, makeNullSafe);
        }

        public DocumentDesign<TEntity> With<TMember>(string name, Expression<Func<TEntity, TMember>> projector, bool makeNullSafe = true)
        {
            Column column;
            Func<object, object> compiledProjector;

            if (makeNullSafe)
            {
                var nullCheckInjector = new NullCheckInjector();
                var nullCheckedProjector = (Expression<Func<TEntity, object>>) nullCheckInjector.Visit(projector);

                column = new Column(name, new SqlColumn(typeof (TMember)))
                {
                    SqlColumn =
                    {
                        Nullable = !nullCheckInjector.CanBeTrustedToNeverReturnNull
                    }
                };

                compiledProjector = Compile(name, nullCheckedProjector);
            }
            else
            {
                column = new Column(name, new SqlColumn(typeof (TMember)));
                compiledProjector = Compile(name, projector);
            }

            var existingColumn = Table.Columns.SingleOrDefault(x => x == column);
            if (existingColumn == null)
            {
                Table.Register(column);
                Projections.Add(column, compiledProjector);
            }
            else
            {
                if (!existingColumn.SqlColumn.Equals(column.SqlColumn))
                    throw new InvalidOperationException("Projection must be of same type.");

                Projections[existingColumn] = compiledProjector;
            }

            return this;
        }

        public DocumentDesign<TEntity> With<TMember>(Expression<Func<TEntity, IEnumerable<TMember>>> projector, bool makeNullSafe = true)
        {
            return this;
        }
    }
}