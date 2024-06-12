using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;
using HybridDb.Migrations.Schema;
using HybridDb.Migrations.Schema.Commands;
using HybridDb.Queue;

namespace HybridDb.Migrations.BuiltIn
{
    public class UpdateCorrelationId : Migration
    {
        public UpdateCorrelationId(int version) : base(version) { }

        public override IEnumerable<DdlCommand> AfterAutoMigrations(Configuration configuration)
        {
            foreach (var table in configuration.Tables.Values.OfType<QueueTable>())
            {
                yield return new SqlCommand(
                    "Update correlation ID",
                    (sql, db) =>
                    {
                        var tableNameEscaped = db.FormatTableNameAndEscape(table.Name);

                        sql.Append(@$"
                            update {tableNameEscaped}
                            set CorrelationId = (select top 1
        		                CorrelationId.value
	                        from openjson(Metadata, '$') with (CorrelationIds nvarchar(max) '$.""{HybridDbMessage.Breadcrumbs}""') X
	                        cross apply openjson(X.CorrelationIds, '$') CorrelationId)");
                    });
            }
        }
    }
}