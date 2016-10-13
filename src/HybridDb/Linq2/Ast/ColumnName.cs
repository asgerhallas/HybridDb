using System;

namespace HybridDb.Linq2.Ast
{
    public class ColumnName : SqlExpression
    {
        //TODO: Add table name
        public ColumnName(string tableName, string identifier)
        {
            TableName = tableName;
            Identifier = identifier;
        }

        public string TableName { get; set; }
        public string Identifier { get; }
    }
}