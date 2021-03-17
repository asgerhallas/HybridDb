using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Migrations.Schema;
using HybridDb.Migrations.Schema.Commands;

namespace HybridDb.Config
{
    public class Table
    {
        readonly Dictionary<string, Column> columns;

        public Table(string name) : this(name, Enumerable.Empty<Column>()) {}
        public Table(string name, params Column[] columns)  : this(name, columns.ToList()) { }

        public Table(string name, IEnumerable<Column> columns)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));

            if (name.EndsWith("_")) throw new NotSupportedException("A table name can not end with '_'.");

            if (columns == null) throw new ArgumentNullException(nameof(columns));

            this.columns = columns.ToDictionary(x => x.Name, x => x);
        }

        public string Name { get; }
        public IEnumerable<Column> Columns => columns.Values;

        public Column this[string name] => columns.TryGetValue(name, out var value) ? value : null;

        public Column<T> Add<T>(Column<T> column)
        {
            columns.Add(column.Name, column);
            return column;
        }

        public virtual DdlCommand GetCreateCommand() => new CreateTable(this);

        public override string ToString() => Name;

        protected bool Equals(Table other) => string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;

            return Equals((Table) obj);
        }

        public override int GetHashCode() => Name.ToLowerInvariant().GetHashCode();
    }
}