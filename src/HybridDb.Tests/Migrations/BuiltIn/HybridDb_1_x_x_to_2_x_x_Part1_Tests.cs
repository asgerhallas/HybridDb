using System;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
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
        public HybridDb_1_x_x_to_2_x_x_Part1_Tests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public void BackgroundCommand_0_10_64_And_Later_MoveAndEncode()
        {
            UseRealTables();

            Setup(SiblingFile("HybridDb_1_x_x_to_2_x_x_Part1_Tests_1.sql"));

            var before = store.Query(new DocumentTable("Cases"), out _).Single();

            ResetConfiguration();

            UseTypeMapper(new OtherTypeMapper());
            UseMigrations(new InlineMigration(1,
                after: ListOf(new HybridDb_1_x_x_to_2_x_x_Part1.UpfrontCommand()),
                background: ListOf(new HybridDb_1_x_x_to_2_x_x_Part1.BackgroundCommand_0_10_64_And_Later())));

            // Match the serializer with the one used in the sample data set
            var serializer = new DefaultSerializer();
            serializer.EnableAutomaticBackReferences();
            UseSerializer(serializer);

            Document<JObject>("Cases", discriminator: "DV.Application.Cases.Case, DV.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");

            TouchStore();

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

            Setup(SiblingFile("HybridDb_1_x_x_to_2_x_x_Part1_Tests_2.sql"));

            ResetConfiguration();

            UseTypeMapper(new OtherTypeMapper());
            UseMigrations(new InlineMigration(1,
                after: ListOf(new HybridDb_1_x_x_to_2_x_x_Part1.UpfrontCommand()),
                background: ListOf(new HybridDb_1_x_x_to_2_x_x_Part1.BackgroundCommand_0_10_64_And_Later())));

            // Match the serializer with the one used in the sample data set
            var serializer = new DefaultSerializer();
            serializer.EnableAutomaticBackReferences();
            UseSerializer(serializer);

            Document<JObject>("Cases", discriminator: "DV.Application.Cases.Case, DV.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");

            TouchStore();

            log.Where(x => x.Exception != null).ShouldBeEmpty();

            var after = store.Query(new DocumentTable("Cases"), out _).Single();

            after["Document"].ShouldBe("{}");
            after["Metadata"].ShouldBe(null);
        }

        [Fact]
        public void BackgroundCommand_Before_0_10_64()
        {
            UseRealTables();

            Setup(SiblingFile("HybridDb_1_x_x_to_2_x_x_Part1_Tests_3.sql"));

            UseTypeMapper(new OtherTypeMapper());
            Document<JObject>("BuildingParts", discriminator: "UValueCalculator.Models.RefBuildingPart, UValueCalculator, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");

            var before = store.Query(new DocumentTable("BuildingParts"), out _).Single();

            ResetStore();

            UseMigrations(new InlineMigration(1,
                after: ListOf(new HybridDb_1_x_x_to_2_x_x_Part1.UpfrontCommand()),
                background: ListOf(new HybridDb_1_x_x_to_2_x_x_Part1.BackgroundCommand_Before_0_10_64())));

            // Match the serializer with the one used in the sample data set
            var serializer = new DefaultSerializer();
            serializer.EnableAutomaticBackReferences();
            UseSerializer(serializer);

            TouchStore();

            var after = store.Query(new DocumentTable("BuildingParts"), out _).Single();

            var expectedDocument = File.ReadAllText("Migrations\\BuiltIn\\HybridDb_1_x_x_to_2_x_x_Part1_Tests_3_Result.json");

            after["Document"].ShouldNotBe(null);
            after["Document"].ShouldBe(expectedDocument);
        }

        public class OtherTypeMapper : ITypeMapper
        {
            public string ToDiscriminator(Type type) => type.Name;
            public Type ToType(Type baseType, string discriminator) => typeof(JObject);
        }
    }
}