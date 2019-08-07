using System;
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

namespace HybridDb.Tests.Migrations.BuiltIn
{
    public class HybridDb_1_x_x_to_2_x_x_Part1_Tests : HybridDbTests
    {
        // TODO: Test at et dokument, der loades uden configureret design via migration ikke forsøges gemt i 
        // databasen i en forkert tabel (Documents) og derved lader migrations gå i uendelig løkke. 
        // Måske skal ManagedEntity have Design på sig?

        [Fact]
        public void Test()
        {
            UseRealTables();

            var commands = File.ReadAllText("Migrations\\Updates\\script.sql").Split(new [] {"GO"}, StringSplitOptions.RemoveEmptyEntries);

            using (var cnn = new SqlConnection(connectionString))
            {
                foreach (var command in commands)
                {
                    cnn.Execute(command);
                }
            }

            var before = store.Query(new DocumentTable("Cases"), out _).Single();

            ResetConfiguration();

            UseTypeMapper(new OtherTypeMapper());
            UseMigrations(new HybridDb_1_x_x_to_2_x_x_Part1(1));

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

        public class A { }

        public class OtherTypeMapper : ITypeMapper
        {
            public string ToDiscriminator(Type type)
            {
                return type.Name;
            }

            public Type ToType(string discriminator)
            {
                return typeof(JObject);
            }
        }

    }
}