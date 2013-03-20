using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HybridDb.Diffing;
using HybridDb.Schema;

namespace HybridDb.Commands
{
    public class UpdateCommand : DatabaseCommand
    {
        protected readonly Guid currentEtag;
        protected readonly byte[] document;
        protected readonly Guid key;
        protected readonly object projections;
        protected readonly bool lastWriteWins;
        protected readonly ITable table;

        public UpdateCommand(ITable table, Guid key, Guid etag, byte[] document, object projections, bool lastWriteWins)
        {
            this.table = table;
            this.key = key;
            currentEtag = etag;
            this.document = document;
            this.projections = projections;
            this.lastWriteWins = lastWriteWins;
        }

        public byte[] Document
        {
            get { return document; }
        }

        internal override PreparedDatabaseCommand Prepare(DocumentStore store, Guid etag, int uniqueParameterIdentifier)
        {
            var values = ConvertAnonymousToProjections(table, projections);

            values.Add(table.EtagColumn, etag);
            values.Add(table.DocumentColumn, document);

            var sql = new SqlBuilder()
                .Append("update {0} set {1} where {2}=@Id{3}",
                        store.Escape(store.GetFormattedTableName(table)),
                        string.Join(", ", from column in values.Keys select column.Name + "=@" + column.Name + uniqueParameterIdentifier),
                        table.IdColumn.Name,
                        uniqueParameterIdentifier)
                .Append(!lastWriteWins, "and {0}=@CurrentEtag{1}",
                        table.EtagColumn.Name,
                        uniqueParameterIdentifier)
                .ToString();

            var parameters = MapProjectionsToParameters(values, uniqueParameterIdentifier);
            parameters.Add(new Parameter { Name = "@Id" + uniqueParameterIdentifier, Value = key, DbType = table.IdColumn.SqlColumn.Type });

            if (!lastWriteWins)
            {
                parameters.Add(new Parameter { Name = "@CurrentEtag" + uniqueParameterIdentifier, Value = currentEtag, DbType = table.EtagColumn.SqlColumn.Type });
            }

            return new PreparedDatabaseCommand
            {
                Sql = sql,
                Parameters = parameters,
                ExpectedRowCount = 1
            };
        }
    }

    public class PatchUpdateCommand : UpdateCommand
    {
        readonly byte[] oldDocument;

        public PatchUpdateCommand(ITable table, Guid key, Guid etag, byte[] oldDocument, byte[] newDocument, object projections, bool lastWriteWins) 
            : base(table, key, etag, newDocument, projections, lastWriteWins)
        {
            this.oldDocument = oldDocument;
        }

        internal override PreparedDatabaseCommand Prepare(DocumentStore store, Guid etag, int uniqueParameterIdentifier)
        {
            var values = ConvertAnonymousToProjections(table, projections);

            values.Add(table.EtagColumn, etag);

            var parameters = MapProjectionsToParameters(values, uniqueParameterIdentifier);

            int i = 0;
            var sql = new SqlBuilder();
            foreach (var delta in CalculateDeltas())
            {
                var paramName = "@Data" + i + "_" + +uniqueParameterIdentifier;

                sql.Append("update {0} set {1} where {2}=@Id{3}",
                           store.Escape(store.GetFormattedTableName(table)),
                           table.DocumentColumn.Name + ".Write(" + paramName + ", " + delta.Offset + ", " + delta.Length + ")",
                           table.IdColumn.Name,
                           uniqueParameterIdentifier);

                parameters.Add(new Parameter { Name = paramName, Value = delta.Data, DbType = table.DocumentColumn.SqlColumn.Type });

                sql.Append(!lastWriteWins, "and {0}=@CurrentEtag{1}",
                           table.EtagColumn.Name,
                           uniqueParameterIdentifier);

                sql.Append(";");

                i++;
            }

            sql.Append("update {0} set {1} where {2}=@Id{3}",
                       store.Escape(store.GetFormattedTableName(table)),
                       string.Join(", ", from column in values.Keys select column.Name + "=@" + column.Name + uniqueParameterIdentifier),
                       table.IdColumn.Name,
                       uniqueParameterIdentifier)
               .Append(!lastWriteWins, "and {0}=@CurrentEtag{1}",
                       table.EtagColumn.Name,
                       uniqueParameterIdentifier);

            parameters.Add(new Parameter {Name = "@Id" + uniqueParameterIdentifier, Value = key, DbType = table.IdColumn.SqlColumn.Type});

            if (!lastWriteWins)
            {
                parameters.Add(new Parameter {Name = "@CurrentEtag" + uniqueParameterIdentifier, Value = currentEtag, DbType = table.EtagColumn.SqlColumn.Type});
            }

            return new PreparedDatabaseCommand
            {
                Sql = sql.ToString(),
                Parameters = parameters,
                ExpectedRowCount = 1 + i
            };
        }

        IEnumerable<Differ.SqlWrite> CalculateDeltas()
        {
            var watch = new Stopwatch();
            watch.Start();
            var calculateDelta = Differ.CalculateDelta(oldDocument, document);
            var diffs = Differ.Squash(calculateDelta).ToList();
            Console.WriteLine("Deltas found: " + diffs.Count());
            Console.WriteLine("Deltas found in " + watch.ElapsedMilliseconds + "ms");
            return diffs;
        }
    }
}