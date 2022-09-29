using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Migrations.Schema;
using HybridDb.Migrations.Schema.Commands;

namespace HybridDb.Config
{
    public class Table
    {
        readonly Dictionary<string, Column> builtInColumns = new();
        readonly Dictionary<string, Column> allColumns;

        public Table(string name) : this(name, Enumerable.Empty<Column>(), Enumerable.Empty<Column>()) {}
        public Table(string name, params Column[] columns)  : this(name, Enumerable.Empty<Column>(), columns.ToList()) { }

        public Table(string name, IEnumerable<Column> builtInColumns, IEnumerable<Column> allColumns)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));

            if (name.EndsWith("_")) throw new NotSupportedException("A table name can not end with '_'.");

            if (allColumns == null) throw new ArgumentNullException(nameof(allColumns));
            if (builtInColumns == null) throw new ArgumentNullException(nameof(builtInColumns));

            this.allColumns = allColumns.ToDictionary(x => x.Name, x => x);
            this.builtInColumns = builtInColumns.ToDictionary(x => x.Name, x => x);

            foreach (var column in this.builtInColumns.Where(column => !this.allColumns.ContainsKey(column.Key)))
            {
                throw new ArgumentException($"AllColumns did not contain BuiltInColumn '{column.Key}'.");
            }
        }

        public string Name { get; }
        public IReadOnlyCollection<Column> Columns => allColumns.Values;
        public IReadOnlyDictionary<string, Column> BuiltInColumns => builtInColumns;

        public Column this[string name] => allColumns.TryGetValue(name, out var value) ? value : null;

        public Column<T> Add<T>(Column<T> column)
        {
            allColumns.Add(column.Name, column);
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