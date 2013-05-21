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
    }
}