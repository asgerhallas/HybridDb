using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;

namespace HybridDb.Schema
{
    public class DocumentDesign
    {
        public DocumentDesign(Configuration configuration, DocumentTable table, Type type)
        {
            Table = table;
            Type = type;
            IndexTables = new Dictionary<Type, IndexTable>();
            Configuration = configuration;
            Projections = new Dictionary<Column, Func<object, object>>
            {
                {Table.IdColumn, document => ((dynamic) document).Id},
                {Table.DocumentColumn, document => configuration.Serializer.Serialize(document)}
            };
        }

        public Type Type { get; private set; }
        public DocumentTable Table { get; private set; }
        public Dictionary<Type, IndexTable> IndexTables { get; private set; }
        public Dictionary<Column, Func<object, object>> Projections { get; private set; }
        public Configuration Configuration { get; private set; }

        public void MigrateSchema()
        {
            Configuration.Store.Migrate(migrator => migrator.MigrateTo(Table));
        }

        protected Func<object, object> Compile<TEntity, TMember>(string name, Expression<Func<TEntity, TMember>> projector)
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

    public class DocumentDesign<TEntity> : DocumentDesign
    {
        public DocumentDesign(Configuration configuration, DocumentTable table) : base(configuration, table, typeof (TEntity)) {}

        public DocumentDesign<TEntity> Project<TMember>(Expression<Func<TEntity, TMember>> projector, bool makeNullSafe = true)
        {
            var name = Configuration.GetColumnNameByConventionFor(projector);
            return Project(name, projector, makeNullSafe);
        }

        public DocumentDesign<TEntity> Project<TMember>(string name, Expression<Func<TEntity, TMember>> projector, bool makeNullSafe = true)
        {
            if (makeNullSafe)
            {
                var nullCheckInjector = new NullCheckInjector();
                var nullCheckedProjector = (Expression<Func<TEntity, object>>) nullCheckInjector.Visit(projector);

                var column = new Column(name, new SqlColumn(typeof (TMember)))
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
                var column = new Column(name, new SqlColumn(typeof (TMember)));
                Table.Register(column);
                Projections.Add(column, Compile(name, projector));
            }

            return this;
        }

        public DocumentDesign<TEntity> Project<TMember>(Expression<Func<TEntity, IEnumerable<TMember>>> projector, bool makeNullSafe = true)
        {
            return this;
        }

        public void Index<TIndex>(string name = null)
        {
            name = name ?? typeof (TIndex).Name;

            var table = Configuration.TryGetIndexTableByName(name) ?? new IndexTable(name);

            Projections.Add(table.TableReferenceColumn, x => Table.Name);

            foreach (var property in typeof (TIndex).GetProperties())
            {
                var projectedProperty = Type.GetProperty(property.Name, property.PropertyType);
                if (projectedProperty == null)
                    continue;

                var parameter = Expression.Parameter(typeof(object));

                var projector =
                    Expression.Lambda<Func<object, object>>(
                        Expression.Convert(
                            Expression.Property(
                                Expression.Convert(parameter, Type),
                                Type.GetProperty(property.Name)),
                            typeof (object)),
                        parameter);

                var columnName = Configuration.GetColumnNameByConventionFor(projector);
                var column = new Column(columnName, new SqlColumn(property.PropertyType));

                table.Register(column);
                Projections.Add(column, Compile(columnName, projector));
            }

            IndexTables.Add(typeof(TIndex), table);
            Configuration.Tables.TryAdd(table.Name, table);
            Configuration.IndexTables.TryAdd(typeof(TIndex), table);
        }
    }
}