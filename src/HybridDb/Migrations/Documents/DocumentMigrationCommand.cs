using System;
using System.Collections.Generic;
using HybridDb.Config;

namespace HybridDb.Migrations.Documents
{
    // 
    // 3. Lav en row-class eller hjælper til at få cells ud af en row via statiske navne på den table man arbejder på 
    // 4. Kan man lave migrations på ting der ikke er document tables?
    // 6. Skal vi overveje at fjerne Migration, og så bare give versioner direkte til migration commands?

    public abstract class RowMigrationCommand
    {
        protected RowMigrationCommand(Type type, string idPrefix)
        {
            Type = type;
            IdPrefix = idPrefix;
        }

        public Type Type { get; }
        public string IdPrefix { get; }

        public virtual string Where => "Version < @version" + (!string.IsNullOrEmpty(IdPrefix) ? " and Id LIKE @idPrefix + '%'" : "");

        public virtual bool IsApplicable(int version, Configuration configuration, IDictionary<string, object> row)
        {
            var rowId = (string)row["Id"];
            var rowType = configuration.TypeMapper.ToType((string) row["Discriminator"]);

            return (int) row["Version"] < version &&
                   (string.IsNullOrEmpty(IdPrefix) || rowId.StartsWith(IdPrefix)) &&
                   (Type == null || Type.IsAssignableFrom(rowType));
        }

        public abstract IDictionary<string, object> Execute(IDocumentSession session, ISerializer serializer, IDictionary<string, object> row);
    }

    public class UpdateProjectionsMigration : RowMigrationCommand
    {
        public UpdateProjectionsMigration() : base(null, null) { }

        public override string Where => "AwaitsReprojection = @AwaitsReprojection";

        public override IDictionary<string, object> Execute(IDocumentSession session, ISerializer serializer, IDictionary<string, object> row) => row;
    }

    public abstract class DocumentMigrationCommand : RowMigrationCommand
    {
        protected DocumentMigrationCommand(Type type, string idPrefix) : base(type, idPrefix)
        {
        }
    }
}