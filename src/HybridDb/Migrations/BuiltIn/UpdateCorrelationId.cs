using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;
using HybridDb.Migrations.Schema;
using HybridDb.Migrations.Schema.Commands;
using HybridDb.Queue;

namespace HybridDb.Migrations.BuiltIn
{
    public class UpdateCorrelationId(int version) : Migration(version)
    {
        public override IEnumerable<DdlCommand> BeforeAutoMigrations(Configuration configuration)
        {
            foreach (var table in configuration.Tables.Values.OfType<QueueTable>())
            {
                yield return new SqlCommand(
                    "Update correlation ID",
                    (sql, db) =>
                    {
                        var breadcrumbsPath = "$.\"" + HybridDbMessage.Breadcrumbs + "\"";

                        sql.Append($"update {table}");
                        sql.Append($@"
                            set CorrelationId = coalesce((select top 1 CorrelationId.value
	                        from openjson(Metadata, '$') with (CorrelationIds nvarchar(max) '{breadcrumbsPath:@}') X
	                        cross apply openjson(X.CorrelationIds, '$') CorrelationId), 'N/A')");
                    });
            }
        }
    }
}