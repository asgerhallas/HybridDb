using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Schema;

namespace HybridDb.Studio.Models
{
    public class Document
    {
        readonly IEnumerable<Projection> projections;
        readonly KeyValuePair<IColumn, object> idColumn;
        readonly KeyValuePair<IColumn, object> documentColumn;
        readonly KeyValuePair<IColumn, object> etagColumn;

        public ITable Table { get; private set; }
        public string DocumentAsString { get; set; }
        
        public IEnumerable<Projection> Projections
        {
            get { return projections; }
        }
        
        public Guid Id
        {
            get { return (Guid) idColumn.Value; }
        }

        public Guid? Etag
        {
            get { return (Guid?)etagColumn.Value; }
        }

        public Document(ITable table, string document, IDictionary<IColumn, object> projections)
        {
            Table = table;
            DocumentAsString = document;
            idColumn = projections.Single(x => x.Key is IdColumn);
            documentColumn = projections.Single(x => x.Key is DocumentColumn);
            etagColumn = projections.Single(x => x.Key is EtagColumn);
            this.projections = projections.Where(x => !IsMetadataColumn(x.Key)).Select(x => new Projection(x.Key, x.Value)).ToList();
        }

        bool IsMetadataColumn(IColumn column)
        {
            return column == idColumn.Key
                   || column == documentColumn.Key
                   || column == etagColumn.Key;
        }
    }
}