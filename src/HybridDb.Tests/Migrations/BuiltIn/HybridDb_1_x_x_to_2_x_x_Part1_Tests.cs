using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using Dapper;
using HybridDb.Config;
using HybridDb.Migrations.BuiltIn;
using HybridDb.Serialization;
using Newtonsoft.Json.Linq;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using static HybridDb.Helpers;

namespace HybridDb.Tests.Migrations.BuiltIn
{
    public class HybridDb_1_x_x_to_2_x_x_Part1_Tests : HybridDbTests
    {
        // TODO: Test at et dokument, der loades uden configureret design via migration ikke fors�ges gemt i 
        // databasen i en forkert tabel (Documents) og derved lader migrations g� i uendelig l�kke. 
        // M�ske skal ManagedEntity have Design p� sig?

        public HybridDb_1_x_x_to_2_x_x_Part1_Tests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public void MoveAndEncode()
        {
            UseRealTables();

            Setup("HybridDb_1_x_x_to_2_x_x_Part1_Tests_1.sql");

            var before = store.Query(new DocumentTable("Cases"), out _).Single();

            ResetConfiguration();

            UseTypeMapper(new OtherTypeMapper());
            UseMigrations(new InlineMigration(1,
                ListOf(new HybridDb_1_x_x_to_2_x_x_Part1.UpfrontCommand()),
                ListOf(new HybridDb_1_x_x_to_2_x_x_Part1.BackgroundCommand())));

            // Match the serializer with the one used in the sample data set
            var serializer = new DefaultSerializer();
            serializer.EnableAutomaticBackReferences();
            UseSerializer(serializer);

            Document<JObject>("Cases");

            InitializeStore();

            var after = store.Query(new DocumentTable("Cases"), out _).Single();

            after["Document"].ShouldNotBe(null);
            after["Document"].ShouldBe(Encoding.UTF8.GetString((byte[])before["Document"]));

            after["Metadata"].ShouldNotBe(null);
            after["Metadata"].ShouldBe(Encoding.UTF8.GetString((byte[])before["Metadata"]));
        }

        [Fact]
        public void HandleANullDocumentAndMetadata()
        {
            UseRealTables();

            Setup("HybridDb_1_x_x_to_2_x_x_Part1_Tests_2.sql");

            ResetConfiguration();

            UseTypeMapper(new OtherTypeMapper());
            UseMigrations(new InlineMigration(1,
                ListOf(new HybridDb_1_x_x_to_2_x_x_Part1.UpfrontCommand()),
                ListOf(new HybridDb_1_x_x_to_2_x_x_Part1.BackgroundCommand())));

            // Match the serializer with the one used in the sample data set
            var serializer = new DefaultSerializer();
            serializer.EnableAutomaticBackReferences();
            UseSerializer(serializer);

            Document<JObject>("Cases");

            InitializeStore();

            log.Where(x => x.Exception != null).ShouldBeEmpty();

            var after = store.Query(new DocumentTable("Cases"), out _).Single();

            after["Document"].ShouldBe("{}");
            after["Metadata"].ShouldBe(null);
        }

        void Setup(string filename)
        {
            var commands = File.ReadAllText($"Migrations\\BuiltIn\\{filename}").Split(new[] {"GO"}, StringSplitOptions.RemoveEmptyEntries);

            using (var cnn = new SqlConnection(connectionString))
            {
                foreach (var command in commands)
                {
                    cnn.Execute(command);
                }
            }

        }

        public class OtherTypeMapper : ITypeMapper
        {
            public string ToDiscriminator(Type type) => type.Name;
            public Type ToType(string discriminator) => typeof(JObject);
        }
    }
}