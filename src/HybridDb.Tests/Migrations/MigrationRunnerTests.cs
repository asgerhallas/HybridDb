using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using HybridDb.Config;
using HybridDb.Logging;
using HybridDb.Migrations;
using HybridDb.Migrations.Commands;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Migrations
{
    public class MigrationRunnerTests : HybridDbStoreTests
    {
        readonly ConsoleLogger logger;

        public MigrationRunnerTests()
        {
            logger = new ConsoleLogger(LogLevel.Debug, new LoggingColors());
        }

        [Fact]
        public void AutomaticallyCreatesMetadataTableWhenUsingRealSchemaDiffer()
        {
            var runner = new MigrationRunner(logger, new SchemaDiffer(), new List<Migration>());

            runner.Migrate(store, configuration);

            configuration.Tables.ShouldContainKey("HybridDb");
            database.RawQuery<int>("select top 1 SchemaVersion from #HybridDb").Single().ShouldBe(0);
        }

        [Fact]
        public void DoesNothingGivenNoMigrations()
        {
            CreateMetadataTable();

            var runner = new MigrationRunner(logger, new FakeSchemaDiffer(), new List<Migration>());

            runner.Migrate(store, configuration);

            database.QuerySchema().Count.ShouldBe(1); // the metadata table and nothing else
        }

        [Fact]
        public void RunsProvidedSchemaMigrations()
        {
            CreateMetadataTable();

            var runner = new MigrationRunner(logger,
                new FakeSchemaDiffer(),
                new InlineMigration(1,
                    new CreateTable(new Table("Testing", new Column("Id", typeof (Guid), isPrimaryKey: true))),
                    new AddColumn("Testing", new Column("Noget", typeof (int)))));

            runner.Migrate(store, configuration);

            var tables = database.QuerySchema();
            tables.ShouldContainKey("Testing");
            tables["Testing"]["Id"].ShouldNotBe(null);
            tables["Testing"]["Noget"].ShouldNotBe(null);
        }

        [Fact]
        public void RunsDiffedSchemaMigrations()
        {
            CreateMetadataTable();

            var runner = new MigrationRunner(logger,
                new FakeSchemaDiffer(
                    new CreateTable(new Table("Testing", new Column("Id", typeof (Guid), isPrimaryKey: true))),
                    new AddColumn("Testing", new Column("Noget", typeof (int)))));

            runner.Migrate(store, configuration);

            var tables = database.QuerySchema();
            tables.ShouldContainKey("Testing");
            tables["Testing"]["Id"].ShouldNotBe(null);
            tables["Testing"]["Noget"].ShouldNotBe(null);
        }

        [Fact]
        public void DoesNotRunUnsafeSchemaMigrations()
        {
            CreateMetadataTable();

            var runner = new MigrationRunner(logger,
                new FakeSchemaDiffer(new UnsafeThrowingCommand()),
                new InlineMigration(1, new UnsafeThrowingCommand()));

            Should.NotThrow<object>(() => runner.Migrate(store, configuration));
        }

        [Fact]
        public void DoesNotRunSchemaMigrationTwice()
        {
            CreateMetadataTable();

            var command = new CountingCommand();

            var runner = new MigrationRunner(logger,
                new FakeSchemaDiffer(), 
                new InlineMigration(1, command));

            runner.Migrate(store, configuration);
            runner.Migrate(store, configuration);

            command.NumberOfTimesCalled.ShouldBe(1);
        }

        [Fact]
        public void NextRunContinuesAtNextVersion()
        {
            CreateMetadataTable();

            var command = new CountingCommand();

            new MigrationRunner(logger, new FakeSchemaDiffer(), new InlineMigration(1, command))
                .Migrate(store, configuration);

            new MigrationRunner(logger,
                new FakeSchemaDiffer(),
                new InlineMigration(1, new ThrowingCommand()),
                new InlineMigration(2, command))
                .Migrate(store, configuration);

            command.NumberOfTimesCalled.ShouldBe(2);
        }

        [Fact]
        public void RollsBackOnExceptions()
        {
            CreateMetadataTable();

            try
            {
                var runner = new MigrationRunner(logger,
                    new FakeSchemaDiffer(
                        new CreateTable(new Table("Testing", new Column("Id", typeof (Guid), isPrimaryKey: true))),
                        new ThrowingCommand()));

                runner.Migrate(store, configuration);
            }
            catch (Exception)
            {
            }

            database.QuerySchema().ShouldNotContainKey("Testing");
        }

        [Fact]
        public void SetsRequiresReprojectionOnTablesWithNewColumns()
        {
            Document<Entity>();
            Document<AbstractEntity>();
            Document<OtherEntity>();

            using (var session = store.OpenSession())
            {
                session.Store(new Entity());
                session.Store(new Entity());
                session.Store(new DerivedEntity());
                session.Store(new DerivedEntity());
                session.Store(new OtherEntity());
                session.Store(new OtherEntity());
                session.SaveChanges();
            }

            var runner = new MigrationRunner(logger, 
                new FakeSchemaDiffer(
                    new AddColumn("Entities", new Column("NewCol", typeof(int))),
                    new AddColumn("AbstractEntities", new Column("NewCol", typeof(int)))));
            
            runner.Migrate(store, configuration);

            database.RawQuery<bool>("select AwaitsReprojection from #Entities").ShouldAllBe(x => x);
            database.RawQuery<bool>("select AwaitsReprojection from #AbstractEntities").ShouldAllBe(x => x);
            database.RawQuery<bool>("select AwaitsReprojection from #OtherEntities").ShouldAllBe(x => !x);
        }

        [Fact]
        public void ContinuesWithReprojectionOfMarkedRows()
        {
            var designer1 = Document<Entity>();
            var designer2 = Document<AbstractEntity>();
            var designer3 = Document<OtherEntity>();

            using (var session = store.OpenSession())
            {
                session.Store(new Entity { Number = 1 });
                session.Store(new Entity { Number = 2 });
                session.Store(new DerivedEntity { Property = "Asger" });
                session.Store(new DerivedEntity { Property = "Peter" });
                session.Store(new OtherEntity { Number = 41 });
                session.Store(new OtherEntity { Number = 42 });
                session.SaveChanges();
            }

            designer1.With(x => x.Number);
            designer2.With(x => x.Property);

            // first run changes the schema, but "fail" before the long running task is started
            new MigrationRunner(logger, new SchemaDiffer())
                .Migrate(store, configuration);

            // second run does not change any schema
            new MigrationRunner(logger, new FakeSchemaDiffer())
                .Migrate(store, configuration)
                .RunSynchronously();

            using (var managedConnection = database.Connect())
            {
                var result1 = managedConnection.Connection.Query<bool, int, Tuple<bool, int>>(
                    "select AwaitsReprojection, Number from #Entities order by Number", Tuple.Create, splitOn: "*").ToList();

                result1[0].ShouldBe(Tuple.Create(false, 1));
                result1[1].ShouldBe(Tuple.Create(false, 2));

                var result2 = managedConnection.Connection.Query<bool, string, Tuple<bool, string>>(
                    "select AwaitsReprojection, Property from #AbstractEntities order by Property", Tuple.Create, splitOn: "*").ToList();
                
                result2[0].ShouldBe(Tuple.Create(false, "Asger"));
                result2[1].ShouldBe(Tuple.Create(false, "Peter"));

                var result3 = managedConnection.Connection.Query<bool>(
                    "select AwaitsReprojection from #OtherEntities").ToList();
                
                result3[0].ShouldBe(false);
                result3[1].ShouldBe(false);
            }
        }

        void CreateMetadataTable()
        {
            new CreateTable(new Table("HybridDb", new Column("SchemaVersion", typeof(int)))).Execute(database);
        }

        public class FakeSchemaDiffer : ISchemaDiffer
        {
            readonly SchemaMigrationCommand[] commands;

            public FakeSchemaDiffer(params SchemaMigrationCommand[] commands)
            {
                this.commands = commands;
            }

            public IReadOnlyList<SchemaMigrationCommand> CalculateSchemaChanges(IReadOnlyList<Table> schema, Configuration configuration)
            {
                return commands.ToList();
            }
        }

        public class ThrowingCommand : SchemaMigrationCommand
        {
            public override void Execute(Database database)
            {
                throw new InvalidOperationException();
            }
        }

        public class UnsafeThrowingCommand : ThrowingCommand
        {
            public UnsafeThrowingCommand()
            {
                Unsafe = true;
            }
        }

        public class CountingCommand : SchemaMigrationCommand
        {
            public int NumberOfTimesCalled { get; private set; }

            public override void Execute(Database database)
            {
                NumberOfTimesCalled++;
            }
        }
    }
}