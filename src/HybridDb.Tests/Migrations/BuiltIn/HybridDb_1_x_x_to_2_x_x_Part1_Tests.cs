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
        // To export data for use in these tests, use "Tasks > Generate Scripts" in SQL Management Studio.
        // Under "Advanced" select "Schema and data" for "Types of data to script"

        // TODO: Test at et dokument, der loades uden configureret design via migration ikke forsøges gemt i 
        // databasen i en forkert tabel (Documents) og derved lader migrations gå i uendelig løkke. 
        // Måske skal ManagedEntity have Design på sig?
        public HybridDb_1_x_x_to_2_x_x_Part1_Tests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public void BackgroundCommand_0_10_64_And_Later_MoveAndEncode()
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

            Document<JObject>("Cases", discriminator: "DV.Application.Cases.Case, DV.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");

            InitializeStore();

            var after = store.Query(new DocumentTable("Cases"), out _).Single();

            after["Document"].ShouldNotBe(null);
            after["Document"].ShouldBe(Encoding.UTF8.GetString((byte[])before["Document"]));

            after["Metadata"].ShouldNotBe(null);
            after["Metadata"].ShouldBe(Encoding.UTF8.GetString((byte[])before["Metadata"]));
        }

        [Fact]
        public void BackgroundCommand_0_10_64_And_Later_HandleANullDocumentAndMetadata()
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

            Document<JObject>("Cases", discriminator: "DV.Application.Cases.Case, DV.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");

            InitializeStore();

            log.Where(x => x.Exception != null).ShouldBeEmpty();

            var after = store.Query(new DocumentTable("Cases"), out _).Single();

            after["Document"].ShouldBe("{}");
            after["Metadata"].ShouldBe(null);
        }

        [Fact]
        public void BackgroundCommand_Before_0_10_64()
        {
            UseRealTables();

            Setup("HybridDb_1_x_x_to_2_x_x_Part1_Tests_3.sql");

            UseTypeMapper(new OtherTypeMapper());
            Document<JObject>("BuildingParts", discriminator: "UValueCalculator.Models.RefBuildingPart, UValueCalculator, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");

            var before = store.Query(new DocumentTable("BuildingParts"), out _).Single();

            ResetStore();

            UseMigrations(new InlineMigration(1,
                ListOf(new HybridDb_1_x_x_to_2_x_x_Part1.UpfrontCommand()),
                ListOf(new HybridDb_1_x_x_to_2_x_x_Part1.BackgroundCommand_Before_0_10_64())));

            // Match the serializer with the one used in the sample data set
            var serializer = new DefaultSerializer();
            serializer.EnableAutomaticBackReferences();
            UseSerializer(serializer);

            InitializeStore();

            var after = store.Query(new DocumentTable("BuildingParts"), out _).Single();

            var expectedDocument = File.ReadAllText("Migrations\\BuiltIn\\HybridDb_1_x_x_to_2_x_x_Part1_Tests_3_Result.json");

            after["Document"].ShouldNotBe(null);
            after["Document"].ShouldBe(expectedDocument);
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
            public Type ToType(Type baseType, string discriminator) => typeof(JObject);
        }
    }
}