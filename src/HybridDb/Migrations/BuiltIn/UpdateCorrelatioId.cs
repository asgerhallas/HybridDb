using System;
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

        public override IEnumerable<DdlCommand> BeforeAutoMigrations(Configuration configuration)
        {
            configuration.Logger.LogMigrationInfo(nameof(UpdateCorrelationId), "Invoked");

            var tables = configuration.Tables.Values.OfType<QueueTable>().ToList();

            if (!tables.Any())
            {
                configuration.Logger.LogMigrationError(nameof(UpdateCorrelationId), "No queue tables registered.");

                throw new InvalidOperationException(
                    $"Migration {nameof(UpdateCorrelationId)} cannot be used when no queue tables are registered.");
            }

            foreach (var table in tables)
            {
                configuration.Logger.LogMigrationInfo(nameof(UpdateCorrelationId), $"Processing table {table.Name}.");

                yield return new SqlCommand(
                    "Update correlation ID",
                    (sql, db) =>
                    {
                        var tableNameEscaped = db.FormatTableNameAndEscape(table.Name);

                        sql.Append(@$"
                            update {tableNameEscaped}
                            set CorrelationId = coalesce((select top 1
        		                CorrelationId.value
	                        from openjson(Metadata, '$') with (CorrelationIds nvarchar(max) '$.""{HybridDbMessage.Breadcrumbs}""') X
	                        cross apply openjson(X.CorrelationIds, '$') CorrelationId), 'N/A')");
                    });
            }
        }
    }
}