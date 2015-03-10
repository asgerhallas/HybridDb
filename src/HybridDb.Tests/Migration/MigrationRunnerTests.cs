using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using HybridDb.Config;
using HybridDb.Logging;
using HybridDb.Migration;
using HybridDb.Migration.Commands;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Migration
{
    public class MigrationRunnerTests : HybridDbDatabaseTests
    {
        readonly ConsoleLogger logger;

        public MigrationRunnerTests()
        {
            logger = new ConsoleLogger(LogLevel.Debug, new LoggingColors());
        }

        [Fact]
        public void AutomaticallyCreatesMetadataTableWhenUsingRealSchemaDiffer()
        {
            var runner = new MigrationRunner(logger, new StaticMigrationProvider(), new SchemaDiffer());

            runner.Migrate(database, configuration); // configuration contains metadata table automatically

            database.RawQuery<int>("select top 1 SchemaVersion from #HybridDb").Single().ShouldBe(0);
        }

        [Fact]
        public void DoesNothingGivenNoMigrations()
        {
            CreateMetadataTable();

            var runner = new MigrationRunner(logger, new StaticMigrationProvider(), new FakeSchemaDiffer());

            runner.Migrate(database, configuration);

            database.QuerySchema().Count.ShouldBe(1); // the metadata table and nothing else
        }

        [Fact]
        public void RunsProvidedSchemaMigrations()
        {
            CreateMetadataTable();

            var runner = new MigrationRunner(
                logger, 
                new StaticMigrationProvider(
                    new InlineMigration(1,
                        new CreateTable(new Table("Testing", new Column("Id", typeof (Guid), new SqlColumn(DbType.Guid, isPrimaryKey: true)))),
                        new AddColumn("Testing", new Column("Noget", typeof (int))))),
                new FakeSchemaDiffer());

            runner.Migrate(database, configuration);

            var tables = database.QuerySchema();
            tables.ShouldContainKey("Testing");
            tables["Testing"]["Id"].ShouldNotBe(null);
            tables["Testing"]["Noget"].ShouldNotBe(null);
        }

        [Fact]
        public void RunsDiffedSchemaMigrations()
        {
            CreateMetadataTable();

            var runner = new MigrationRunner(
                logger, 
                new StaticMigrationProvider(),
                new FakeSchemaDiffer(
                    new CreateTable(new Table("Testing", new Column("Id", typeof (Guid), new SqlColumn(DbType.Guid, isPrimaryKey: true)))),
                    new AddColumn("Testing", new Column("Noget", typeof (int)))));

            runner.Migrate(database, configuration);

            var tables = database.QuerySchema();
            tables.ShouldContainKey("Testing");
            tables["Testing"]["Id"].ShouldNotBe(null);
            tables["Testing"]["Noget"].ShouldNotBe(null);
        }

        [Fact]
        public void DoesNotRunUnsafeSchemaMigrations()
        {
            CreateMetadataTable();

            var runner = new MigrationRunner(
                logger, 
                new StaticMigrationProvider(
                    new InlineMigration(1, new UnsafeThrowingCommand())),
                new FakeSchemaDiffer(
                    new UnsafeThrowingCommand()));

            Should.NotThrow(() => runner.Migrate(database, configuration));
        }

        [Fact]
        public void DoesNotRunSchemaMigrationTwice()
        {
            CreateMetadataTable();

            var command = new CountingCommand();

            var runner = new MigrationRunner(
                logger,
                new StaticMigrationProvider(
                    new InlineMigration(1, command)),
                new FakeSchemaDiffer());

            runner.Migrate(database, configuration);
            runner.Migrate(database, configuration);

            command.NumberOfTimesCalled.ShouldBe(1);
        }

        [Fact]
        public void RollsBackOnExceptions()
        {
            CreateMetadataTable();

            try
            {
                var runner = new MigrationRunner(
                    logger,
                    new StaticMigrationProvider(),
                    new FakeSchemaDiffer(
                        new CreateTable(new Table("Testing", new Column("Id", typeof(Guid), new SqlColumn(DbType.Guid, isPrimaryKey: true)))),
                        new ThrowingCommand()));

                runner.Migrate(database, configuration);
            }
            catch (Exception)
            {
            }

            database.QuerySchema().ShouldNotContainKey("Testing");
        }

        void CreateMetadataTable()
        {
            new CreateTable(new Table("HybridDb", new Column("SchemaVersion", typeof(int)))).Execute(database);
        }

        public class InlineMigration : HybridDb.Migration.Migration
        {
            readonly MigrationCommand[] commands;

            public InlineMigration(int version, params MigrationCommand[] commands) : base(version)
            {
                this.commands = commands;
            }

            public override IEnumerable<MigrationCommand> Migrate()
            {
                return commands;
            }
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