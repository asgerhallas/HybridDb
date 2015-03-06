using System.Collections.Generic;

namespace HybridDb
{
    public interface ISchema
    {
        bool TableExists(string name);
        List<string> GetTables();
        Schema.Column GetColumn(string tablename, string columnname);
        string GetType(int id);
        bool IsPrimaryKey(string column);
    }
}