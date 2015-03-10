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
    //tx = new TransactionScope(TransactionScopeOption.Required, new TransactionOptions {IsolationLevel = IsolationLevel.Serializable});
    public class MigrationRunnerTests : HybridDbTests
    {
        readonly ConsoleLogger logger;

        public MigrationRunnerTests()
        {
            logger = new ConsoleLogger(LogLevel.Debug, new LoggingColors());
        }

        [Fact]
        public void DoesNothingGivenNoMigrations()
        {
            var runner = new MigrationRunner(logger, new StaticMigrationProvider(), new FakeSchemaDiffer());

            runner.Migrate(store);

            store.Schema.GetSchema().ShouldBeEmpty();
        }

        [Fact]
        public void RunsProvidedSchemaMigrations()
        {
            var runner = new MigrationRunner(
                logger, 
                new StaticMigrationProvider(
                    new InlineMigration(1,
                        new CreateTable(new Table("Testing", new Column("Id", typeof (Guid), new SqlColumn(DbType.Guid, isPrimaryKey: true)))),
                        new AddColumn("Testing", new Column("Noget", typeof (int))))),
                new FakeSchemaDiffer());

            runner.Migrate(store);

            var tables = store.Schema.GetSchema();
            tables.ShouldContainKey("Testing");
            tables["Testing"]["Id"].ShouldNotBe(null);
            tables["Testing"]["Noget"].ShouldNotBe(null);
        }

        [Fact]
        public void RunsDiffedSchemaMigrations()
        {
            var runner = new MigrationRunner(
                logger, 
                new StaticMigrationProvider(),
                new FakeSchemaDiffer(
                    new CreateTable(new Table("Testing", new Column("Id", typeof (Guid), new SqlColumn(DbType.Guid, isPrimaryKey: true)))),
                    new AddColumn("Testing", new Column("Noget", typeof (int)))));

            runner.Migrate(store);

            var tables = store.Schema.GetSchema();
            tables.ShouldContainKey("Testing");
            tables["Testing"]["Id"].ShouldNotBe(null);
            tables["Testing"]["Noget"].ShouldNotBe(null);
        }

        [Fact]
        public void DoesNotRunUnsafeSchemaMigrations()
        {
            var runner = new MigrationRunner(
                logger, 
                new StaticMigrationProvider(
                    new InlineMigration(1, new UnsafeThrowingCommand())),
                new FakeSchemaDiffer(
                    new UnsafeThrowingCommand()));

            Should.NotThrow(() => runner.Migrate(store));
        }

        [Fact]
        public void DoesNotRunSchemaMigrationTwice()
        {
            var command = new CountingCommand();

            var runner = new MigrationRunner(
                logger, 
                new StaticMigrationProvider(
                    new InlineMigration(1, command)),
                new FakeSchemaDiffer());

            runner.Migrate(store);
            runner.Migrate(store);

            command.NumberOfTimesCalled.ShouldBe(1);
        }

        [Fact]
        public void RollsBackOnExceptions()
        {
            var command = new CountingCommand();

            var runner = new MigrationRunner(
                logger,
                new StaticMigrationProvider(
                    new InlineMigration(1, command)),
                new FakeSchemaDiffer());

            runner.Migrate(store);
            runner.Migrate(store);

            command.NumberOfTimesCalled.ShouldBe(1);
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

        public class UnsafeThrowingCommand : SchemaMigrationCommand
        {
            public UnsafeThrowingCommand()
            {
                Unsafe = true;
            }

            public override void Execute(DocumentStore store)
            {
                throw new InvalidOperationException();
            }
        }

        public class CountingCommand : SchemaMigrationCommand
        {
            public int NumberOfTimesCalled { get; private set; }

            public override void Execute(DocumentStore store)
            {
                NumberOfTimesCalled++;
            }
        }
    }
}