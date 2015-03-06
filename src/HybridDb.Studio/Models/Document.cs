using System;
using System.Collections.Generic;
using System.Linq;
using HybridDb.Configuration;

namespace HybridDb.Studio.Models
{
    public class Document
    {
        readonly IEnumerable<Projection> projections;
        readonly KeyValuePair<string, object> idColumn;
        readonly KeyValuePair<string, object> documentColumn;
        readonly KeyValuePair<string, object> etagColumn;

        public DocumentTable Table { get; private set; }
        public string DocumentAsString { get; set; }
        
        public IEnumerable<Projection> Projections
        {
            get { return projections; }
        }
        
        public Guid Id
        {
            get { return (Guid) idColumn.Value; }
        }

        public string Name
        {
            get { return Table.Name + "/" + Id.ToString().Substring(0, 8); }
        }

        public Guid? Etag
        {
            get { return (Guid?)etagColumn.Value; }
        }

        public Document(DocumentTable table, string document, IDictionary<string, object> projections)
        {
            Table = table;
            DocumentAsString = document;
            idColumn = projections.Single(x => x.Key == table.IdColumn.Name);
            documentColumn = projections.Single(x => x.Key == table.DocumentColumn.Name);
            etagColumn = projections.Single(x => x.Key == table.EtagColumn.Name);
            this.projections = projections.Where(x => !(x.Key is SystemColumn))
                                          .Select(x => new Projection(x.Key, x.Value))
                                          .ToList();
        }
    }
}