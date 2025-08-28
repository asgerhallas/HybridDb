using System.Data;
using HybridDb.Commands;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests.Commands
{
    public class SqlCommandTests(ITestOutputHelper output) : HybridDbTests(output)
    {
        [Fact]
        public void SqlCommand_Success()
        {
            Document<Entity>();

            var tableName = store.Database.FormatTableNameAndEscape(store.Configuration.GetDesignFor<Entity>().Table.Name);

            using var updateSession = store.OpenSession();

            var entityId = NewId();

            updateSession.Store(new Entity { Id = entityId, Field = "Initial Value" });
            updateSession.Store(new Entity { Id = NewId(), Field = "Initial Value" });

            updateSession.SaveChanges();

            var sql = new SqlBuilder();

            sql.Append($"update {tableName} set Document = @Document where Id = @Id");
            sql.Parameters.Add("@Document", "{\"Field\":\"Updated Value\"}", SqlDbType.NVarChar);
            sql.Parameters.Add("@Id", entityId, SqlDbType.NVarChar);

            updateSession.Advanced.DocumentStore.Execute(new SqlCommand(sql, expectedRowCount: 1));

            using var readSession = store.OpenSession();

            readSession.Load<Entity>(entityId).Field.ShouldBe("Updated Value");
        }

        [Fact]
        public void SqlCommand_Fail_IncorrectExpectedRowCount()
        {
            Document<Entity>();

            var tableName = store.Database.FormatTableNameAndEscape(store.Configuration.GetDesignFor<Entity>().Table.Name);

            using var updateSession = store.OpenSession();

            updateSession.Store(new Entity { Id = NewId(), Field = "Initial Value" });
            updateSession.Store(new Entity { Id = NewId(), Field = "Initial Value" });

            updateSession.SaveChanges();

            var sql = new SqlBuilder();

            sql.Append($"update {tableName} set Document = @Document");
            sql.Parameters.Add("@Document", null, SqlDbType.NVarChar);

            Should.Throw<ConcurrencyException>(() => updateSession.Advanced.DocumentStore.Execute(new SqlCommand(sql, expectedRowCount: 1)))
                .Message
                .ShouldBe(
                    "Someone beat you to it. Expected 1 changes, but got 2. The transaction is rolled back now, so no changes were actually made.");
        }
    }
}