using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using System.Linq;

namespace HybridDb.Schema
{
    public class DocumentDesign
    {
        public DocumentDesign(Configuration configuration, DocumentTable table, Type type)
        {
            Configuration = configuration;
            Table = table;
            Type = type;
            Projections = new Dictionary<string, Func<object, object>>
            {
                {Table.IdColumn, document => ((dynamic) document).Id},
                {Table.DocumentColumn, document => configuration.Serializer.Serialize(document)}
            };
            Indexes = new Dictionary<IndexTable, Dictionary<string, Func<object, object>>>();
        }

        public Type Type { get; private set; }
        public DocumentTable Table { get; private set; }
        public Dictionary<IndexTable, Dictionary<string, Func<object, object>>> Indexes { get; private set; }
        public Dictionary<string, Func<object, object>> Projections { get; private set; }
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

        public IndexDesigner<TIndex> Index<TIndex>(string name = null)
        {
            name = name ?? typeof (TIndex).Name;

            var table = Configuration.TryGetIndexTableByName(name);
            if (table == null)
            {
                table = new IndexTable(name);

                foreach (var property in typeof (TIndex).GetProperties())
                {
                    var column = new Column(property.Name, new SqlColumn(property.PropertyType));

                    if (!table.Columns.Contains(column))
                        table.Register(column);
                }
            }

            var projections = new Dictionary<string, Func<object, object>>
            {
                {table.IdColumn, x => ((dynamic) x).Id},
                {table.TableReferenceColumn, x => Table.Name}
            };

            AddDefaultProjections<TIndex>(table, projections);

            Indexes.Add(table, projections);
            Configuration.Tables.TryAdd(table.Name, table);
            Configuration.IndexTables.TryAdd(typeof (TIndex), table);

            return new IndexDesigner<TIndex>(this, table);
        }

        void AddDefaultProjections<TIndex>(IndexTable table, Dictionary<string, Func<object, object>> projections)
        {
            foreach (var column in table.Columns)
            {
                if (projections.ContainsKey(column.Name))
                    continue;

                var projectedProperty = Type.GetProperty(column.Name);
                if (projectedProperty == null)
                    continue;

                var indexProperty = typeof (TIndex).GetProperty(column.Name, projectedProperty.PropertyType);
                if (indexProperty == null)
                {
                    throw new ArgumentException(
                        string.Format("Type of property named {0} on index {1} does not match type of property on document {2}",
                            column.Name, typeof (TIndex).Name, typeof (TEntity).Name));
                }

                var parameter = Expression.Parameter(typeof (object));

                var projector =
                    Expression.Lambda<Func<object, object>>(
                        Expression.Convert(
                            Expression.Property(
                                Expression.Convert(parameter, Type),
                                Type.GetProperty(column.Name)),
                            typeof (object)),
                        parameter);

                projections.Add(column.Name, Compile(column.Name, projector));
            }
        }

        public class IndexDesigner<TIndex>
        {
            readonly DocumentDesign<TEntity> design;
            readonly IndexTable table;

            public IndexDesigner(DocumentDesign<TEntity> design, IndexTable table)
            {
                this.design = design;
                this.table = table;
            }

            public IndexDesigner<TIndex> With<TMember>(Expression<Func<TIndex, TMember>> property, Expression<Func<TEntity, TMember>> projector)
            {
                var nullCheckInjector = new NullCheckInjector();
                var nullCheckedProjector = (Expression<Func<TEntity, object>>)nullCheckInjector.Visit(projector);

                var columnName = ((MemberExpression)property.Body).Member.Name;
                design.Indexes[table].Add(table[columnName], design.Compile(columnName, nullCheckedProjector));

                return this;
            }

            public IndexDesigner<T> Index<T>(string name = null)
            {
                return design.Index<T>(name);
            }
        }
    }
}