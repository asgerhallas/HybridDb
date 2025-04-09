using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HybridDb.Config;
using HybridDb.Migrations.Schema;
using HybridDb.Migrations.Schema.Commands;
using HybridDb.SqlBuilder;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using static HybridDb.Helpers;

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

            configuration.Tables.Keys.ShouldContain("HybridDb");
            store.Database.RawQuery<int>(Sql.From("select top 1 SchemaVersion from HybridDb")).Single().ShouldBe(0);
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

            UseMigrations(new InlineMigration(1, after: ListOf<DdlCommand>(
                new CreateTable(new Table("Testing", new Column("Id", typeof (Guid), isPrimaryKey: true))),
                new AddColumn("Testing", new Column("Noget", typeof (int))))));

            var runner = new SchemaMigrationRunner(store, new FakeSchemaDiffer());

            runner.Run();

            var tables = store.Database.QuerySchema();
            tables.ShouldContainKey("Testing");
            tables["Testing"].ShouldContain("Id");
            tables["Testing"].ShouldContain("Noget");
        }

        [Fact]
        public void DoesRunProvidedSchemaMigrationsOnTempTables()
        {
            UseGlobalTempTables();

            UseTableNamePrefix(Guid.NewGuid().ToString());
            CreateMetadataTable();

            EnableUpfrontMigrationsOnTempTables();

            UseMigrations(new InlineMigration(1, after: ListOf<DdlCommand>(
                new CreateTable(new Table("Testing", new Column("Id", typeof(Guid), isPrimaryKey: true))),
                new AddColumn("Testing", new Column("Noget", typeof(int))))));

            var runner = new SchemaMigrationRunner(store, new FakeSchemaDiffer());

            runner.Run();

            var tables = store.Database.QuerySchema();
            tables.ShouldContainKey("Testing");
        }

        [Fact]
        public void BeforeAutoMigration_WithAutoMigrations()
        {
            UseTableNamePrefix(Guid.NewGuid().ToString());
            CreateMetadataTable();
            Document<Entity>("Testing")
                .With(x => x.Property);

            UseMigrations(new InlineMigration(1, before: ListOf<DdlCommand>(
                new CreateTable(new Table("Testing", new Column("Id", typeof(Guid), isPrimaryKey: true))),
                new AddColumn("Testing", new Column("Noget", typeof(int))))));

            var runner = new SchemaMigrationRunner(store, new SchemaDiffer());

            runner.Run();

            var tables = store.Database.QuerySchema();
            var table = tables["Testing"];
            table.ShouldContain("Property");
            table.ShouldContain("Noget");
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
        public void RunsInThreeSteps_Before_Auto_After()
        {
            CreateMetadataTable();

            var table = new Table("Testing", new Column("Id", typeof(Guid), isPrimaryKey: true));

            UseMigrations(
                new InlineMigration(1, 
                    before: ListOf<DdlCommand>(
                        new CreateTable(table),
                        new AddColumn("Testing", new Column("Noget", typeof(int)))),
                    after: ListOf<DdlCommand>(new RenameColumn(table, "NogetNyt", "NogetVirkeligNyt"))));

            var runner = new SchemaMigrationRunner(store,
                new FakeSchemaDiffer(new RenameColumn(table, "Noget", "NogetNyt")));

            runner.Run();

            var tables = store.Database.QuerySchema();
            tables.ShouldContainKey("Testing");
            tables["Testing"].ShouldContain("Id");
            tables["Testing"].ShouldNotContain("Noget");
            tables["Testing"].ShouldNotContain("NogetNyt");
            tables["Testing"].ShouldContain("NogetVirkeligNyt");
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

            UseMigrations(new InlineMigration(1, after: ListOf<DdlCommand>(command)));

            var runner = new SchemaMigrationRunner(store, new FakeSchemaDiffer());

            runner.Run();

            command.NumberOfTimesCalled.ShouldBe(1);
        }

        [Fact]
        public void DoesNotRunSchemaMigrationTwice()
        {
            CreateMetadataTable();

            var command = new CountingCommand();

            UseMigrations(new InlineMigration(1, after: ListOf<DdlCommand>(command)));

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

            UseMigrations(new InlineMigration(1, after: ListOf<DdlCommand>(command)));

            new SchemaMigrationRunner(store, new FakeSchemaDiffer()).Run();

            ResetConfiguration();

            UseMigrations(
                new InlineMigration(1, after: ListOf<DdlCommand>(new ThrowingCommand())), 
                new InlineMigration(2, after: ListOf<DdlCommand>(command)));

            new SchemaMigrationRunner(store, new FakeSchemaDiffer()).Run();

            command.NumberOfTimesCalled.ShouldBe(2);
        }

        [Fact]
        public void ThrowsIfSchemaVersionIsAhead()
        {
            CreateMetadataTable();

            UseMigrations(new InlineMigration(1, after: ListOf<DdlCommand>(new CountingCommand())));

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
                new InlineMigration(1, after: ListOf<DdlCommand>(countingCommand)), 
                new InlineMigration(2, after: ListOf<DdlCommand>(countingCommand)));

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

            store.Database.RawQuery<bool>(Sql.From("select AwaitsReprojection from Entities")).ShouldAllBe(x => x == true);
            store.Database.RawQuery<bool>(Sql.From("select AwaitsReprojection from AbstractEntities")).ShouldAllBe(x => x == true);
            store.Database.RawQuery<bool>(Sql.From("select AwaitsReprojection from OtherEntities")).ShouldAllBe(x => x == false);
        }

        [Fact]
        public void HandlesConcurrentRuns()
        {
            TouchStore();

            var command = new CountingCommand();

            Func<SchemaMigrationRunner> runnerFactory = () => 
                new SchemaMigrationRunner(store, new SchemaDiffer());

            UseMigrations(new InlineMigration(1, after: ListOf<DdlCommand>(
                new AddColumn("Other", new Column("Asger", typeof(int))),
                new CreateTable(new Table("Testing", new Column("Id", typeof(Guid), isPrimaryKey: true))),
                new SlowCommand(TimeSpan.FromSeconds(60)),
                command)));

            store.Execute(new CreateTable(new DocumentTable("Other")));

            Parallel.For(1, 10, x =>
            {
                output.WriteLine(Thread.CurrentThread.ManagedThreadId.ToString());
                if (string.IsNullOrEmpty(Thread.CurrentThread.Name)) Thread.CurrentThread.Name = $"Test thread {x}";
                runnerFactory().Run(); 
            });

            command.NumberOfTimesCalled.ShouldBe(1);
        }

        [Fact]
        public void HandlesConcurrentRuns_MultipleServers_CounterPart()
        {
            var parentConnectionString = Environment.GetEnvironmentVariable($"{nameof(HandlesConcurrentRuns_MultipleServers)}:ConnectionString");

            if (parentConnectionString == null) return;

            var documentStore = DocumentStore.ForTesting(
                TableMode.RealTables,
                x =>
                {
                    x.UseConnectionString(parentConnectionString);
                },
                initialize: false);

            var command = new CountingCommand();

            documentStore.Configuration.UseMigrations(ListOf(new InlineMigration(1, after: ListOf<DdlCommand>(command))));

            new SchemaMigrationRunner(documentStore, new SchemaDiffer()).Run();

            command.NumberOfTimesCalled.ShouldBe(0);
        }

        [Fact]
        public async Task HandlesConcurrentRuns_MultipleServers()
        {
            TouchStore();

            var processStartInfo = new ProcessStartInfo("dotnet")
            {
                Arguments = $"test HybridDb.Tests.dll --filter {nameof(HandlesConcurrentRuns_MultipleServers_CounterPart)}",
                EnvironmentVariables = { [$"{nameof(HandlesConcurrentRuns_MultipleServers)}:ConnectionString"] = connectionString },
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            var command = new CountingCommand();

            UseMigrations(
                new InlineMigration(
                    1,
                    after: ListOf<DdlCommand>(
                        new SlowCommand(TimeSpan.FromSeconds(60)),
                        command)));

            var task = Task.Run(() => new SchemaMigrationRunner(store, new SchemaDiffer()).Run());

            using (var process = Process.Start(processStartInfo))
            {
                await task;

                var readToEnd = await process.StandardOutput.ReadToEndAsync();
                
                await process.WaitForExitAsync();

                output.WriteLine(readToEnd);

                readToEnd.ShouldContain("Passed:     1");
            }

            command.NumberOfTimesCalled.ShouldBe(1);
        }

        void CreateMetadataTable() => store.Execute(
            new CreateTable(new Table("HybridDb",
                new Column("SchemaVersion", typeof(int)))));

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
            public SlowCommand(TimeSpan? howSlow = null)
            {
                HowSlow = howSlow ?? TimeSpan.FromMilliseconds(5000);
                Safe = true;
            }
            public TimeSpan HowSlow { get; }

            public override void Execute(DocumentStore store) => Thread.Sleep(HowSlow);

            public override string ToString() => "";
        }
    }
}