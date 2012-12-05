using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using System.Linq;
using Newtonsoft.Json;

namespace HybridDb
{
    public class TableConfiguration<TEntity> : ITableConfiguration
    {
        readonly Dictionary<string, IColumnConfiguration> columns;

        public TableConfiguration(JsonSerializer serializer)
        {
            columns = new Dictionary<string, IColumnConfiguration>();
            Name = typeof (TEntity).Name;

            IdColumn = new IdColumn();
            columns.Add(IdColumn.Name, IdColumn);

            EtagColumn = new EtagColumn();
            columns.Add(EtagColumn.Name, EtagColumn);

            DocumentColumn = new DocumentColumn(serializer);
            columns.Add(DocumentColumn.Name, DocumentColumn);
        }

        public EtagColumn EtagColumn { get; private set; }
        public IdColumn IdColumn { get; private set; }
        public DocumentColumn DocumentColumn { get; private set; }

        public IColumnConfiguration this[string name]
        {
            get { return columns[name]; }
        }

        public string Name { get; private set; }

        public IEnumerable<IColumnConfiguration> Columns
        {
            get { return columns.Values; }
        }

        public TableConfiguration<TEntity> Store<TMember>(Expression<Func<TEntity, TMember>> member)
        {
            var column = new ColumnConfiguration<TEntity, TMember>(member);
            columns.Add(column.Name, column);
            return this;
        }
    }

    public class EtagColumn : IColumnConfiguration
    {
        public string Name { get { return "Etag"; } }
        public Column Column { get { return new Column(DbType.Guid); } }
        public object GetValue(object document)
        {
            return Guid.NewGuid();
        }
    }
}