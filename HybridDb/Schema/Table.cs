using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace HybridDb.Schema
{
    public class Table<TEntity> : ITable
    {
        readonly Dictionary<string, IColumn> columns;

        public Table()
        {
            columns = new Dictionary<string, IColumn>();
            Name = typeof (TEntity).Name;

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

        public IColumn this[string name]
        {
            get
            {
                IColumn value;
                if (columns.TryGetValue(name, out value))
                    return value;

                return null;
            }
        }

        public string Name { get; private set; }

        public IEnumerable<IColumn> Columns
        {
            get { return columns.Values; }
        }

        public Table<TEntity> Projection<TMember>(Expression<Func<TEntity, TMember>> member)
        {
            var column = new ProjectionColumn<TEntity, TMember>(member);
            columns.Add(column.Name, column);
            return this;
        }
    }
}