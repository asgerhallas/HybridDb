using System;
using System.Collections.Generic;
using System.Linq;

namespace HybridDb.Config
{
    public class Table
    {
        readonly Dictionary<string, Column> columns;

        public Table(string name) : this(name, Enumerable.Empty<Column>()) {}
        public Table(string name, params Column[] columns)  : this(name, columns.ToList()) { }

        public Table(string name, IEnumerable<Column> columns)
        {
            if (name.EndsWith("_"))
            {
                throw new NotSupportedException("A table name can not end with '_'.");
            }

            Name = name;

            this.columns = columns.ToDictionary(x => x.Name, x => x);
        }

        public Column this[string name] => columns.TryGetValue(name, out var value) ? value : null;

        public virtual Column this[KeyValuePair<string, object> namedValue] => this[namedValue.Key];

        public string Name { get; }

        public IEnumerable<Column> Columns => columns.Values;

        public void Register(Column column) => columns.Add(column.Name, column);

        public override string ToString() => Name;
    }
}