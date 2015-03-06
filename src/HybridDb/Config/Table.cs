using System.Collections.Generic;

namespace HybridDb.Config
{
    public class Table
    {
        readonly Dictionary<string, Column> columns;

        public Table(string name)
        {
            columns = new Dictionary<string, Column>();
            Name = name;
        }

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