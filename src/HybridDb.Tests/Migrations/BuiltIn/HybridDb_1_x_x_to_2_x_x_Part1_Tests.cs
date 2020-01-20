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
using static HybridDb.Helpers;

namespace HybridDb.Tests.Migrations.BuiltIn
{
    public class HybridDb_1_x_x_to_2_x_x_Part1_Tests : HybridDbTests
    {
        // To export data for use in these tests, use "Tasks > Generate Scripts" in SQL Management Studio.
        // Under "Advanced" select "Schema and data" for "Types of data to script"

        // TODO: Test at et dokument, der loades uden configureret design via migration ikke forsøges gemt i 
        // databasen i en forkert tabel (Documents) og derved lader migrations gå i uendelig løkke. 
        // Måske skal ManagedEntity have Design på sig?
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
                ListOf(new HybridDb_1_x_x_to_2_x_x_Part1.BackgroundCommand_0_10_64_And_Later())));

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

        [Fact(Skip = "Skip for now. Needs better setup and assertion.")]
        public void Test3()
        {
            UseRealTables();

            Setup("HybridDb_1_x_x_to_2_x_x_Part1_Tests_3.sql");

            UseTypeMapper(new OtherTypeMapper());
            Document<JObject>("BuildingParts");

            var before = store.Query(new DocumentTable("BuildingParts"), out _).Single();

            ResetConfiguration();

            UseTypeMapper(new OtherTypeMapper());
            UseMigrations(new InlineMigration(1,
                ListOf(new HybridDb_1_x_x_to_2_x_x_Part1.UpfrontCommand()),
                ListOf(new HybridDb_1_x_x_to_2_x_x_Part1.BackgroundCommand_Before_0_10_64())));

            // Match the serializer with the one used in the sample data set
            var serializer = new DefaultSerializer();
            serializer.EnableAutomaticBackReferences();
            UseSerializer(serializer);

            Document<JObject>("BuildingParts");

            InitializeStore();

            var after = store.Query(new DocumentTable("BuildingParts"), out _).Single();

            after["Document"].ShouldNotBe(null);
            after["Document"].ShouldBe(Encoding.UTF8.GetString((byte[])before["Document"]));
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
                ListOf(new HybridDb_1_x_x_to_2_x_x_Part1.BackgroundCommand_0_10_64_And_Later())));

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