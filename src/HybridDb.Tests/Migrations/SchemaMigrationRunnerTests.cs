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
        [Fact]
        public void AutomaticallyCreatesMetadataTable()
        {
            var runner = new SchemaMigrationRunner(store, new SchemaDiffer());

            runner.Run(false);

            configuration.tables.ShouldContainKey("HybridDb");
            store.Database.RawQuery<int>($"select top 1 SchemaVersion from {store.Database.FormatTableNameAndEscape("HybridDb")}").Single().ShouldBe(0);
        }

        [Fact]
        public void DoesNothingWhenTurnedOff()
        {
            DisableMigrations();
            CreateMetadataTable();

            var runner = new SchemaMigrationRunner(store, new SchemaDiffer());

            runner.Run(false);

            configuration.tables.ShouldNotContainKey("HybridDb");
            store.Database.RawQuery<int>($"select top 1 SchemaVersion from {store.Database.FormatTableNameAndEscape("HybridDb")}").Any().ShouldBe(false);
        }

        [Fact]
        public void DoesNothingGivenNoMigrations()
        {
            CreateMetadataTable();

            var runner = new SchemaMigrationRunner(store, new FakeSchemaDiffer());

            runner.Run(false);

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

            runner.Run(false);

            var tables = store.Database.QuerySchema();
            tables.ShouldContainKey("Testing");
            tables["Testing"].ShouldContain("Id");
            tables["Testing"].ShouldContain("Noget");
        }

        [Fact]
        public void DoesNotRunProvidedSchemaMigrationsWhenTesting()
        {
            CreateMetadataTable();

            UseMigrations(new InlineMigration(1,
                new CreateTable(new Table("Testing", new Column("Id", typeof(Guid), isPrimaryKey: true))),
                new AddColumn("Testing", new Column("Noget", typeof(int)))));

            var runner = new SchemaMigrationRunner(store, new FakeSchemaDiffer());

            runner.Run(true);

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

            runner.Run(false);

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

            runner.Run(false);

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

            Should.NotThrow(() => runner.Run(false));
        }

        [Fact]
        public void DoesNotRunSchemaMigrationTwice()
        {
            CreateMetadataTable();

            var command = new CountingCommand();

            UseMigrations(new InlineMigration(1, command));

            var runner = new SchemaMigrationRunner(store, new FakeSchemaDiffer());

            runner.Run(false);
            runner.Run(false);

            command.NumberOfTimesCalled.ShouldBe(1);
        }

        [Fact]
        public void NextRunContinuesAtNextVersion()
        {
            CreateMetadataTable();

            var command = new CountingCommand();

            UseMigrations(new InlineMigration(1, command));

            new SchemaMigrationRunner(store, new FakeSchemaDiffer()).Run(false);

            Reset();

            UseMigrations(new InlineMigration(1, new ThrowingCommand()), new InlineMigration(2, command));

            new SchemaMigrationRunner(store, new FakeSchemaDiffer()).Run(false);

            command.NumberOfTimesCalled.ShouldBe(2);
        }

        [Fact]
        public void ThrowsIfSchemaVersionIsAhead()
        {
            CreateMetadataTable();

            UseMigrations(new InlineMigration(1, new CountingCommand()));

            new SchemaMigrationRunner(store, new FakeSchemaDiffer()).Run(false);

            Reset();

            Should.Throw<InvalidOperationException>(() => new SchemaMigrationRunner(store, new FakeSchemaDiffer()).Run(false))
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

                runner.Run(false);
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
                session.SaveChanges();
            }

            var runner = new SchemaMigrationRunner(store, 
                new FakeSchemaDiffer(
                    new AddColumn("Entities", new Column("NewCol", typeof(int))),
                    new AddColumn("AbstractEntities", new Column("NewCol", typeof(int)))));
            
            runner.Run(false);

            store.Database.RawQuery<bool>($"select AwaitsReprojection from {store.Database.FormatTableNameAndEscape("Entities")}").ShouldAllBe(x => x);
            store.Database.RawQuery<bool>($"select AwaitsReprojection from {store.Database.FormatTableNameAndEscape("AbstractEntities")}").ShouldAllBe(x => x);
            store.Database.RawQuery<bool>($"select AwaitsReprojection from {store.Database.FormatTableNameAndEscape("OtherEntities")}").ShouldAllBe(x => !x);
        }

        [Fact]
        public void HandlesConcurrentRuns()
        {
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

            Execute(new CreateTable(new DocumentTable("Other")));

            Parallel.For(1, 10, x =>
            {
                Console.WriteLine(Thread.CurrentThread.ManagedThreadId);
                runnerFactory().Run(false);
            });

            countingCommand.NumberOfTimesCalled.ShouldBe(1);
        }

        void CreateMetadataTable()
        {
            Execute(new CreateTable(new Table("HybridDb", 
                new Column("SchemaVersion", typeof(int)))));
        }

        public class FakeSchemaDiffer : ISchemaDiffer
        {
            readonly SchemaMigrationCommand[] commands;

            public FakeSchemaDiffer(params SchemaMigrationCommand[] commands) => this.commands = commands;
            public IReadOnlyList<SchemaMigrationCommand> CalculateSchemaChanges(IReadOnlyDictionary<string, List<string>> schema, Configuration configuration) => commands.ToList();
        }

        public class ThrowingCommand : SchemaMigrationCommand
        {
            public override void Execute(IDatabase database) => throw new InvalidOperationException();
            public override string ToString() => "";
        }

        public class UnsafeThrowingCommand : ThrowingCommand
        {
            public UnsafeThrowingCommand() => Unsafe = true;
        }

        public class CountingCommand : SchemaMigrationCommand
        {
            public int NumberOfTimesCalled { get; private set; }

            public override void Execute(IDatabase database) => NumberOfTimesCalled++;
            public override string ToString() => "";
        }
        
        public class SlowCommand : SchemaMigrationCommand
        {
            public override void Execute(IDatabase database) => Thread.Sleep(5000);
            public override string ToString() => "";
        }
    }
}