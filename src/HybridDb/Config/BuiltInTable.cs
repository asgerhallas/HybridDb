using System.Collections.Generic;
using System.Linq;

namespace HybridDb.Config
{
    public abstract class BuiltInTable<TTable> : Table
    {
        // ReSharper disable once StaticMemberInGenericType
        // We want a dictionary per closed generic type. 
        protected static readonly List<Column> staticBuiltInColumns = new();

        protected BuiltInTable(string name, IEnumerable<Column> columns) 
            : base(name, staticBuiltInColumns, columns.Concat(staticBuiltInColumns)) { }

        protected static Column<TColumn> AddBuiltIn<TColumn>(Column<TColumn> column)
        {
            staticBuiltInColumns.Add(column);
            return column;
        }
    }
}