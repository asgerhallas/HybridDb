using System;
using System.Collections.Generic;
using System.Data;

namespace HybridDb.Schema
{
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

            CreatedAtColumn = new SystemColumn("CreatedAt", new SqlColumn(DbType.DateTimeOffset));
            Register(CreatedAtColumn);

            ModifiedAtColumn = new SystemColumn("ModifiedAt", new SqlColumn(DbType.DateTimeOffset));
            Register(ModifiedAtColumn);
        }

        public SystemColumn IdColumn { get; private set; }
        public SystemColumn EtagColumn { get; private set; }
        public SystemColumn CreatedAtColumn { get; private set; }
        public SystemColumn ModifiedAtColumn { get; private set; }

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

        public virtual Column this[KeyValuePair<string, object> namedValue]
        {
            get { return this[namedValue.Key]; }
        }

        //public virtual Column GetColumn(string name, Type type)
        //{
        //    Column column;
        //    if (columns.TryGetValue(name, out column))
        //        return column;

        //    return new Column(name, type);
        //}

        public string Name { get; private set; }

        public IEnumerable<Column> Columns
        {
            get { return columns.Values; }
        }

        public void Register(Column column)
        {
            Column existingColumn;
            if (columns.TryGetValue(column.Name, out existingColumn))
            {
                if (!Equals(existingColumn.SqlColumn, column.SqlColumn))
                    throw new ColumnAlreadRegisteredException(this, column);
            }
            else
            {
                columns.Add(column.Name, column);
            }
        }
    }

    public class DynamicTable : Table
    {
        public DynamicTable(string name) : base(name) {}

        public override Column this[KeyValuePair<string, object> namedValue]
        {
            get { return this[namedValue.Key] ?? new Column(namedValue.Key, namedValue.Value.GetTypeOrDefault()); }
        }
    }
}