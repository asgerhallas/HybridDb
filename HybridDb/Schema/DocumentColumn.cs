using System;
using System.Data;

namespace HybridDb.Schema
{
    public class DocumentColumn : IColumn
    {
        public string Name
        {
            get { return "Document"; }
        }

        public Column Column
        {
            get { return new Column(DbType.Binary, Int32.MaxValue); }
        }

        public object Serialize(object value)
        {
            return value;
        }
    }
}