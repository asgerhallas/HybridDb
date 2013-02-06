using System.Collections.Generic;

namespace HybridDb.Schema
{
    public class Table : ITable
    {
        readonly Dictionary<string, Column> columns;

        public Table(string name)
        {
            columns = new Dictionary<string, Column>();
            Name = name;

            IdColumn = new IdColumn();
            columns.Add(IdColumn.Name, IdColumn);

            EtagColumn = new EtagColumn();
            columns.Add(EtagColumn.Name, EtagColumn);

            DocumentColumn = new DocumentColumn();
            columns.Add(DocumentColumn.Name, DocumentColumn);
        }

        public EtagColumn EtagColumn { get; private set; }
        public IdColumn IdColumn { get; private set; }
        public DocumentColumn DocumentColumn { get; private set; }
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

        public Column GetNamedOrDynamicColumn(string name, object value)
        {
            Column column;
            if (columns.TryGetValue(name, out column))
                return column;

            return value == null 
                ? new DynamicColumn(name)
                : new DynamicColumn(name, value.GetType());
        }

        public string Name { get; private set; }

        public IEnumerable<Column> Columns
        {
            get { return columns.Values; }
        }

        public void AddProjection(ProjectionColumn column)
        {
            columns.Add(column.Name, column);
        }
    }
}