using System.Collections.Generic;
using HybridDb.Config;

namespace HybridDb
{
    public interface ISchema
    {
        bool TableExists(string name);
        List<string> GetTables();
        Column GetColumn(string tablename, string columnname);
        bool IsPrimaryKey(string column);
    }
}