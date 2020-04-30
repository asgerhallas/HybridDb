using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HybridDb.Config;
using HybridDb.Migrations.Schema;
using HybridDb.Migrations.Schema.Commands;
using ShinySwitch;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests.Migrations
{
    public class SchemaMigrationRunnerTests : HybridDbTests
    {
        public SchemaMigrationRunnerTests(ITestOutputHelper output) : base(output)
        {
            NoInitialize();
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
            var runner = new SchemaMigrationRunner(store, new FakeSchemaDiffer());

            runner.Run();

            var schemaAfter = store.Database.QuerySchema();

            // Documents table is not created because of the fake differ
            schemaAfter.Count.ShouldBe(1);
            schemaAfter.ShouldContainKey("HybridDb");
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
        [InlineData(TableMode.GlobalTempTables)]
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

        [Fact(Skip="We are experimenting with doing it the other way around to enable adding indexes to newly created tables.")]
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
        public void UnsafeSchemaMigrations_NoRunWhenInferred()
        {
            CreateMetadataTable();

            var command = new UnsafeCountingCommand();

            var runner = new SchemaMigrationRunner(store, new FakeSchemaDiffer(command));

            runner.Run();

            command.NumberOfTimesCalled.ShouldBe(0);

        }

        [Fact]
        public void UnsafeSchemaMigrations_RunsWhenProvided()
        {
            CreateMetadataTable();

            var command = new UnsafeCountingCommand();

            UseMigrations(new InlineMigration(1, command));

            var runner = new SchemaMigrationRunner(store, new FakeSchemaDiffer());

            runner.Run();

            command.NumberOfTimesCalled.ShouldBe(1);
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

            ResetConfiguration();

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

            ResetConfiguration();

            Should.Throw<InvalidOperationException>(() => new SchemaMigrationRunner(store, new FakeSchemaDiffer()).Run())
                .Message.ShouldBe("Database schema is ahead of configuration. Schema is version 1, but the highest migration version number is 0.");
        }

        [Fact]
        public void MultipleMigrationsAtTheSameTime()
        {
            CreateMetadataTable();

            var countingCommand = new CountingCommand();

            UseMigrations(
                new InlineMigration(1, countingCommand), 
                new InlineMigration(2, countingCommand));

            Should.NotThrow(() => new SchemaMigrationRunner(store, new FakeSchemaDiffer()).Run());

            countingCommand.NumberOfTimesCalled.ShouldBe(2);
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
                session.SaveChanges();
            }

            ResetStore();

            var runner = new SchemaMigrationRunner(store, 
                new FakeSchemaDiffer(
                    new AddColumn("Entities", new Column("NewCol", typeof(int))),
                    new AddColumn("AbstractEntities", new Column("NewCol", typeof(int)))));
            
            runner.Run();

            store.Database.RawQuery<bool>("select AwaitsReprojection from Entities").ShouldAllBe(x => x == true);
            store.Database.RawQuery<bool>("select AwaitsReprojection from AbstractEntities").ShouldAllBe(x => x == true);
            store.Database.RawQuery<bool>("select AwaitsReprojection from OtherEntities").ShouldAllBe(x => x == false);
        }

        [Fact]
        public void HandlesConcurrentRuns()
        {
            output.WriteLine("XXX");

            InitializeStore();

            var command = new CountingCommand();

            Func<SchemaMigrationRunner> runnerFactory = () => 
                new SchemaMigrationRunner(store, new SchemaDiffer());

            UseMigrations(new InlineMigration(1,
                new AddColumn("Other", new Column("Asger", typeof(int))),
                new CreateTable(new Table("Testing", new Column("Id", typeof(Guid), isPrimaryKey: true))),
                new SlowCommand(),
                command));

            store.Execute(new CreateTable(new DocumentTable("Other")));

            Parallel.For(1, 10, x =>
            {
                output.WriteLine(Thread.CurrentThread.ManagedThreadId.ToString());
                if (string.IsNullOrEmpty(Thread.CurrentThread.Name)) Thread.CurrentThread.Name = $"Test thread {x}";
                runnerFactory().Run(); 
            });

            command.NumberOfTimesCalled.ShouldBe(1);
        }

        void CreateMetadataTable() => store.Execute(
            new CreateTable(new Table("HybridDb",
                new Column("SchemaVersion", typeof(int)))));

        void Setup<T>(Func<DocumentStore, Action<T>> match) =>
            configuration.Decorate<Func<DocumentStore, DdlCommand, Action>>((container, decoratee) => (documentStore, command) => () =>
                Switch.On(command)
                    .Match(match(documentStore))
                    .Else(_ => decoratee(documentStore, command)()));

        public class FakeSchemaDiffer : ISchemaDiffer
        {
            readonly DdlCommand[] commands;

            public FakeSchemaDiffer(params DdlCommand[] commands) => this.commands = commands;

            public IReadOnlyList<DdlCommand> CalculateSchemaChanges(IReadOnlyDictionary<string, List<string>> schema, Configuration configuration) => 
                commands.ToList();
        }

        public class ThrowingCommand : DdlCommand
        {
            public ThrowingCommand() => Safe = true;

            public override void Execute(DocumentStore store) => throw new InvalidOperationException("ThrowingCommand");

            public override string ToString() => "";
        }

        public class CountingCommand : DdlCommand
        {
            public CountingCommand() => Safe = true;

            public int NumberOfTimesCalled { get; private set; }

            public override void Execute(DocumentStore store) => NumberOfTimesCalled++;
            public override string ToString() => "";
        }

        public class UnsafeCountingCommand : CountingCommand
        {
            public UnsafeCountingCommand() => Safe = false;
        }

        public class SlowCommand : DdlCommand
        {
            public SlowCommand() => Safe = true;

            public override void Execute(DocumentStore store) => Thread.Sleep(5000);

            public override string ToString() => "";
        }
    }
}