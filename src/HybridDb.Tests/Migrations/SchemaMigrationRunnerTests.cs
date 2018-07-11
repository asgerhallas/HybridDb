using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HybridDb.Config;
using HybridDb.Migrations;
using HybridDb.Migrations.Commands;
using Shouldly;
using Xunit;
using Xunit.Extensions;

namespace HybridDb.Tests.Migrations
{
    public class SchemaMigrationRunnerTests : HybridDbTests
    {
        public SchemaMigrationRunnerTests()
        {
            UseRealTables();
        }

        [Fact]
        public void AutomaticallyCreatesMetadataTable()
        {
            var runner = new SchemaMigrationRunner(store, new SchemaDiffer());

            runner.Run();

            configuration.tables.ShouldContainKey("HybridDb");
            store.Database.RawQuery<int>("select top 1 SchemaVersion from HybridDb").Single().ShouldBe(0);
        }

        [Fact]
        public void DoesNothingWhenTurnedOff()
        {
            DisableMigrations();
            CreateMetadataTable();

            var runner = new SchemaMigrationRunner(store, new SchemaDiffer());

            runner.Run();

            configuration.tables.ShouldNotContainKey("HybridDb");
            store.Database.RawQuery<int>("select top 1 SchemaVersion from HybridDb").Any().ShouldBe(false);
        }

        [Fact]
        public void DoesNothingGivenNoMigrations()
        {
            CreateMetadataTable();

            var runner = new SchemaMigrationRunner(store, new FakeSchemaDiffer());

            runner.Run();

            var schema = store.Database.QuerySchema();
            schema.Count.ShouldBe(1);
            schema.ShouldContainKey("HybridDb"); // the metadata table and nothing else
        }

        [Fact]
        public void RunsProvidedSchemaMigrations()
        {
            CreateMetadataTable();

            UseMigrations(new InlineMigration(1,
                new CreateTable(new Table("Testing", new Column("Id", typeof (Guid), isPrimaryKey: true))),
                new AddColumn("Testing", new Column("Noget", typeof (int)))));

            var runner = new SchemaMigrationRunner(store, new FakeSchemaDiffer());

            runner.Run();

            var tables = store.Database.QuerySchema();
            tables.ShouldContainKey("Testing");
            tables["Testing"].ShouldContain("Id");
            tables["Testing"].ShouldContain("Noget");
        }

        [Theory]
        [InlineData(TableMode.UseTempTables)]
        [InlineData(TableMode.UseTempDb)]
        public void DoesNotRunProvidedSchemaMigrationsOnTempTables(TableMode mode)
        {
            Use(mode);

            UseTableNamePrefix(Guid.NewGuid().ToString());
            CreateMetadataTable();

            UseMigrations(new InlineMigration(1,
                new CreateTable(new Table("Testing", new Column("Id", typeof(Guid), isPrimaryKey: true))),
                new AddColumn("Testing", new Column("Noget", typeof(int)))));

            var runner = new SchemaMigrationRunner(store, new FakeSchemaDiffer());

            runner.Run();

            var tables = store.Database.QuerySchema();
            tables.ShouldNotContainKey("Testing");
        }

        [Fact]
        public void RunsDiffedSchemaMigrations()
        {
            CreateMetadataTable();

            var runner = new SchemaMigrationRunner(store,
                new FakeSchemaDiffer(
                    new CreateTable(new Table("Testing", new Column("Id", typeof (Guid), isPrimaryKey: true))),
                    new AddColumn("Testing", new Column("Noget", typeof (int)))));

            runner.Run();

            var tables = store.Database.QuerySchema();
            tables.ShouldContainKey("Testing");
            tables["Testing"].ShouldContain("Id");
            tables["Testing"].ShouldContain("Noget");
        }

        [Fact]
        public void RunsProvidedSchemaMigrationsInOrderThenDiffed()
        {
            CreateMetadataTable();

            var table = new Table("Testing", new Column("Id", typeof(Guid), isPrimaryKey: true));

            UseMigrations(
                new InlineMigration(2, new AddColumn("Testing", new Column("Noget", typeof(int)))),
                new InlineMigration(1, new CreateTable(table)));

            var runner = new SchemaMigrationRunner(store,
                new FakeSchemaDiffer(new RenameColumn(table, "Noget", "NogetNyt")));

            runner.Run();

            var tables = store.Database.QuerySchema();
            tables.ShouldContainKey("Testing");
            tables["Testing"].ShouldContain("Id");
            tables["Testing"].ShouldContain("NogetNyt");
        }
        

        [Fact]
        public void DoesNotRunUnsafeSchemaMigrations()
        {
            CreateMetadataTable();

            UseMigrations(new InlineMigration(1, new UnsafeThrowingCommand()));

            var runner = new SchemaMigrationRunner(store,
                new FakeSchemaDiffer(new UnsafeThrowingCommand()));

            Should.NotThrow(() => runner.Run());
        }

        [Fact]
        public void DoesNotRunSchemaMigrationTwice()
        {
            CreateMetadataTable();

            var command = new CountingCommand();

            UseMigrations(new InlineMigration(1, command));

            var runner = new SchemaMigrationRunner(store, new FakeSchemaDiffer());

            runner.Run();
            runner.Run();

            command.NumberOfTimesCalled.ShouldBe(1);
        }

        [Fact]
        public void NextRunContinuesAtNextVersion()
        {
            CreateMetadataTable();

            var command = new CountingCommand();

            UseMigrations(new InlineMigration(1, command));

            new SchemaMigrationRunner(store, new FakeSchemaDiffer()).Run();

            Reset();

            UseMigrations(new InlineMigration(1, new ThrowingCommand()), new InlineMigration(2, command));

            new SchemaMigrationRunner(store, new FakeSchemaDiffer()).Run();

            command.NumberOfTimesCalled.ShouldBe(2);
        }

        [Fact]
        public void ThrowsIfSchemaVersionIsAhead()
        {
            CreateMetadataTable();

            UseMigrations(new InlineMigration(1, new CountingCommand()));

            new SchemaMigrationRunner(store, new FakeSchemaDiffer()).Run();

            Reset();

            Should.Throw<InvalidOperationException>(() => new SchemaMigrationRunner(store, new FakeSchemaDiffer()).Run())
                .Message.ShouldBe("Database schema is ahead of configuration. Schema is version 1, but configuration is version 0.");
        }

        [Fact]
        public void RollsBackOnExceptions()
        {
            CreateMetadataTable();

            try
            {
                var runner = new SchemaMigrationRunner(store,
                    new FakeSchemaDiffer(
                        new CreateTable(new Table("Testing", new Column("Id", typeof (Guid), isPrimaryKey: true))),
                        new ThrowingCommand()));

                runner.Run();
            }
            catch (Exception)
            {
            }

            store.Database.QuerySchema().ShouldNotContainKey("Testing");
        }

        [Fact]
        public void SetsRequiresReprojectionOnTablesWithNewColumns()
        {
            Document<Entity>();
            Document<AbstractEntity>();
            Document<DerivedEntity>();
            Document<OtherEntity>();

            store.Initialize();

            using (var session = store.OpenSession())
            {
                session.Store(new Entity());
                session.Store(new Entity());
                session.Store(new DerivedEntity());
                session.Store(new DerivedEntity());
                session.Store(new OtherEntity());
                session.Store(new OtherEntity());
                session.SaveChangesAsync();
            }

            var runner = new SchemaMigrationRunner(store, 
                new FakeSchemaDiffer(
                    new AddColumn("Entities", new Column("NewCol", typeof(int))),
                    new AddColumn("AbstractEntities", new Column("NewCol", typeof(int)))));
            
            runner.Run();

            store.Database.RawQuery<bool>("select AwaitsReprojection from Entities").ShouldAllBe(x => x);
            store.Database.RawQuery<bool>("select AwaitsReprojection from AbstractEntities").ShouldAllBe(x => x);
            store.Database.RawQuery<bool>("select AwaitsReprojection from OtherEntities").ShouldAllBe(x => !x);
        }

        [Fact]
        public void HandlesConcurrentRuns()
        {
            UseRealTables();
            CreateMetadataTable();

            store.Initialize();

            var countingCommand = new CountingCommand();

            Func<SchemaMigrationRunner> runnerFactory = () => 
                new SchemaMigrationRunner(store, new SchemaDiffer());

            UseMigrations(new InlineMigration(1,
                new AddColumn("Other", new Column("Asger", typeof(int))),
                new CreateTable(new Table("Testing", new Column("Id", typeof(Guid), isPrimaryKey: true))),
                new SlowCommand(),
                countingCommand));

            new CreateTable(new DocumentTable("Other")).Execute(store.Database);

            Parallel.For(1, 10, x =>
            {
                Console.WriteLine(Thread.CurrentThread.ManagedThreadId);
                runnerFactory().Run();
            });

            countingCommand.NumberOfTimesCalled.ShouldBe(1);
        }

        void CreateMetadataTable()
        {
            new CreateTable(new Table("HybridDb", 
                new Column("SchemaVersion", typeof(int))))
                .Execute(store.Database);
        }

        public class FakeSchemaDiffer : ISchemaDiffer
        {
            readonly SchemaMigrationCommand[] commands;

            public FakeSchemaDiffer(params SchemaMigrationCommand[] commands)
            {
                this.commands = commands;
            }

            public IReadOnlyList<SchemaMigrationCommand> CalculateSchemaChanges(IReadOnlyDictionary<string, List<string>> schema, Configuration configuration)
            {
                return commands.ToList();
            }
        }

        public class ThrowingCommand : SchemaMigrationCommand
        {
            public override void Execute(IDatabase database)
            {
                throw new InvalidOperationException();
            }

            public override string ToString()
            {
                return "";
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

            public override void Execute(IDatabase database)
            {
                NumberOfTimesCalled++;
            }

            public override string ToString()
            {
                return "";
            }
        }
        
        public class SlowCommand : SchemaMigrationCommand
        {
            public override void Execute(IDatabase database)
            {
                Thread.Sleep(5000);
            }

            public override string ToString()
            {
                return "";
            }
        }
    }
}