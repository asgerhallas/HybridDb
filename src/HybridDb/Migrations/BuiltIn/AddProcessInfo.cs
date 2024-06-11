using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;
using HybridDb.Migrations.Schema;
using HybridDb.Migrations.Schema.Commands;
using HybridDb.Queue;

namespace HybridDb.Migrations.BuiltIn
{
    public class AddProcessInfo : Migration
    {
        public AddProcessInfo(int version) : base(version) { }

        public override IEnumerable<DdlCommand> AfterAutoMigrations(Configuration configuration)
        {
            foreach (var table in configuration.Tables.Values.OfType<QueueTable>())
            {
                yield return new SqlCommand(
                    "Add ProcessInfo",
                    (sql, db) =>
                    {
                        var tableNameEscaped = db.FormatTableNameAndEscape(table.Name);

                        sql.Append(@$"
                            update {tableNameEscaped}
                            set ProcessInfo = (select
        		                CorrelationId.value
	                        from openjson(Metadata, '$') with (CorrelationIds nvarchar(max) '$.""correlation-ids""') X
	                        cross apply openjson(X.CorrelationIds, '$') CorrelationId
        	                where CorrelationId.value like 'Process/%')");
                    });
            }
        }
    }
}