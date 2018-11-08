using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using HybridDb.Config;

namespace HybridDb.Migrations.Commands
{
    public class ChangeColumnType : SchemaMigrationCommand
    {
        public ChangeColumnType(string tableName, Column column)
        {
            Unsafe = true;
            TableName = tableName;
            Column = column;
        }

        public string TableName { get; }
        public Column Column { get; }

        public override void Execute(IDatabase database)
        {
            throw new NotSupportedException("Please remove/add column through a migration.");
        }

        public override string ToString() => $"Change type of column {Column} on table {TableName} to {Column.Type}.";
    }
}