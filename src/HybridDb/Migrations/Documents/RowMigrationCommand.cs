using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using HybridDb.Config;

namespace HybridDb.Migrations.Documents
{
    // 8. Part2 - eller muligheden for at lave schema ændringer efter doc?
    // 4. Kan man lave migrations på ting der ikke er document tables?

    public abstract class RowMigrationCommand
    {
        public abstract bool Matches(Configuration configuration, Table table);
        public abstract SqlBuilder Matches(int? version);
    }


    public abstract class RowMigrationCommand<T> : RowMigrationCommand
    {
        public abstract bool IsApplicable(int version, Configuration configuration, Row<T> row);
        public abstract IDictionary<string, object> Execute(IDocumentSession session, ISerializer serializer, Row<T> row);
    }

    public abstract class DocumentRowMigrationCommand : RowMigrationCommand<DocumentTable>
    {
        protected DocumentRowMigrationCommand(Type type, string idPrefix)
        {
            Type = type;
            IdPrefix = idPrefix;
        }

        public Type Type { get; }
        public string IdPrefix { get; }

        public override bool Matches(Configuration configuration, Table table) => 
            table is DocumentTable && (
                Type == null || configuration.TryGetDesignFor(Type)?.Table == table
            );

        public override SqlBuilder Matches(int? version) => new SqlBuilder()
            .Append(version != null, "Version < @version", new Parameter("version", version))
            .Append(!string.IsNullOrEmpty(IdPrefix), " and Id LIKE @idPrefix + '%'", new Parameter("idPrefix", IdPrefix));

        public override bool IsApplicable(int version, Configuration configuration, Row<DocumentTable> row)
        {
            var rowId = (string)row.Get(x => x.IdColumn);
            var rowType = configuration.TypeMapper.ToType((string)row[DocumentTable.IdColumn]);

            return (int)row["Version"] < version &&
                   (string.IsNullOrEmpty(IdPrefix) || rowId.StartsWith(IdPrefix)) &&
                   (Type == null || Type.IsAssignableFrom(rowType));
        }

        //public abstract IDictionary<string, object> Execute(IDocumentSession session, ISerializer serializer, Row<DocumentTable> row);
    }
}