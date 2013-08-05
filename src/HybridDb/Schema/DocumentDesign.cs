using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace HybridDb.Schema
{
    public class DocumentDesign
    {
        readonly DocumentStore store;
        protected readonly Configuration configuration;

        public DocumentDesign(DocumentStore store, Configuration configuration, DocumentTable table, Type type)
        {
            this.store = store;
            this.configuration = configuration;
            Table = table;
            Type = type;
            Projections = new Dictionary<Column, Func<object, object>>
            {
                {Table.IdColumn, document => ((dynamic) document).Id},
                {Table.DocumentColumn, document => configuration.Serializer.Serialize(document)}
            };
        }

        public DocumentTable Table { get; private set; }
        public Type Type { get; private set; }
        public Dictionary<Column, Func<object, object>> Projections { get; private set; }

        public void MigrateSchema()
        {
            store.Migrate(migrator => migrator.MigrateTo(Table));
        }
    }

    public class DocumentDesign<TEntity> : DocumentDesign
    {
        public DocumentDesign(DocumentStore store, Configuration configuration, DocumentTable table) : base(store, configuration, table, typeof(TEntity)) { }

        public DocumentDesign<TEntity> Project<TMember>(Expression<Func<TEntity, TMember>> projector, bool makeNullSafe = true)
        {
            var name = configuration.GetColumnNameByConventionFor(projector);
            return Project(name, projector, makeNullSafe);
        }

        public DocumentDesign<TEntity> Project<TMember>(string name, Expression<Func<TEntity, TMember>> projector, bool makeNullSafe = true)
        {
            if (makeNullSafe)
            {
                var nullCheckInjector = new NullCheckInjector();
                var nullCheckedProjector = (Expression<Func<TEntity, object>>) nullCheckInjector.Visit(projector);

                var column = new Column(name, new SqlColumn(typeof(TMember)))
                {
                    SqlColumn =
                    {
                        Nullable = !nullCheckInjector.CanBeTrustedToNeverReturnNull
                    }
                };

                Table.Register(column);
                Projections.Add(column, Compile(name, nullCheckedProjector));
            }
            else
            {
                var column = new Column(name, new SqlColumn(typeof(TMember)));
                Table.Register(column);
                Projections.Add(column, Compile(name, projector));
            }

            return this;
        }

        public DocumentDesign<TEntity> Project<TMember>(Expression<Func<TEntity, IEnumerable<TMember>>> projector, bool makeNullSafe = true)
        {
            return this;
        }

        public static Expression<Func<TModel, object>> InjectNullChecks<TModel, TProperty>(Expression<Func<TModel, TProperty>> expression)
        {
            return (Expression<Func<TModel, object>>)new NullCheckInjector().Visit(expression);
        }

        Func<object, object> Compile<TMember>(string name, Expression<Func<TEntity, TMember>> projector)
        {
            return x =>
            {
                try
                {
                    var compiled = projector.Compile();
                    return (object) compiled((TEntity) x);
                }
                catch (Exception ex)
                {
                    throw new TargetInvocationException(
                        string.Format("The projector for column {0} threw an exception.\nThe projector code is {1}.", name, projector), ex);
                }
            };
        }
    }
}