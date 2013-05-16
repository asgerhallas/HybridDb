using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;

namespace HybridDb.Schema
{
    public class DocumentConfiguration
    {
        public DocumentConfiguration(Table table, Type type)
        {
            Table = table;
            Type = type;
            Projections = new Dictionary<Column, Func<object, object>>
            {
                {Table.IdColumn, document => ((dynamic) document).Id}
            };
        }

        public Table Table { get; private set; }
        public Type Type { get; private set; }
        public Dictionary<Column, Func<object, object>> Projections { get; private set; }
    }

    public class DocumentConfiguration<TEntity> : DocumentConfiguration
    {
        public DocumentConfiguration(Table table) : base(table, typeof(TEntity)) { }

        public DocumentConfiguration<TEntity> Project<TMember>(Expression<Func<TEntity, TMember>> projector)
        {
            var expression = projector.ToString();
            var name = string.Join("", expression.Split('.').Skip(1));
            return Project(name, projector);
        }

        public DocumentConfiguration<TEntity> Project<TMember>(string columnName, Expression<Func<TEntity, TMember>> projector)
        {
            var column = new UserColumn(columnName, new SqlColumn(typeof(TMember)));
            Table.Register(column);
            
            var compiledProjector = Cast(projector).Compile();
            Projections.Add(column, compiledProjector);

            return this;
        }

        public DocumentConfiguration<TEntity> Project<TMember>(string columnName, Expression<Func<TEntity, IEnumerable<TMember>>> projector)
        {
            //var column = new UserColumn(columnName, new SqlColumn(typeof(TMember)));
            //Table.Register(column);
            
            //var compiledProjector = Cast(projector).Compile();
            //Projections.Add(column, compiledProjector);

            return this;
        }

        public static Expression<Func<object, object>> Cast<TModel, TFromProperty>(Expression<Func<TModel, TFromProperty>> expression)
        {
            var parameterAsObject = Expression.Parameter(typeof (object));

            var wrappedCall =
                    Expression.Convert(
                        Expression.Invoke(
                            expression,
                            Expression.Convert(parameterAsObject, typeof (TModel))),
                        typeof (object));

            return Expression.Lambda<Func<object, object>>(wrappedCall, parameterAsObject);
        }
    }

    public class Table
    {
        readonly Dictionary<string, Column> columns;

        public Table(string name)
        {
            columns = new Dictionary<string, Column>();
            Name = name;

            IdColumn = new SystemColumn("Id", new SqlColumn(DbType.Guid, isPrimaryKey: true));
            Register(IdColumn);

            EtagColumn = new SystemColumn("Etag", new SqlColumn(DbType.Guid));
            Register(EtagColumn);

            DocumentColumn = new SystemColumn("Document", new SqlColumn(DbType.Binary, Int32.MaxValue));
            Register(DocumentColumn);
        }

        public SystemColumn IdColumn { get; private set; }
        public SystemColumn EtagColumn { get; private set; }
        public SystemColumn DocumentColumn { get; private set; }
        public object SizeColumn { get; private set; }
        public object CreatedAtColumn { get; private set; }

        public Column this[string name]
        {
            get
            {
                Column value;
                if (columns.TryGetValue(name, out value))
                    return value;

                return null;
            }
        }

        public Column GetColumnOrDefaultDynamicColumn(string name, Type type)
        {
            Column column;
            if (columns.TryGetValue(name, out column))
                return column;

            return type == null 
                ? new UserColumn(name)
                : new UserColumn(name, type);
        }

        public string Name { get; private set; }

        public IEnumerable<Column> Columns
        {
            get { return columns.Values; }
        }

        public void Register(Column column)
        {
            columns.Add(column.Name, column);
        }

        public string GetFormattedName(TableMode tableMode)
        {
            switch (tableMode)
            {
                case TableMode.UseRealTables:
                    return Name;
                case TableMode.UseTempTables:
                    return "#" + Name;
                case TableMode.UseGlobalTempTables:
                    return "##" + Name;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}