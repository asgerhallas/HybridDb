using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Transactions;
using HybridDb.Config;
using HybridDb.Migrations;
using HybridDb.Migrations.Commands;
using Newtonsoft.Json.Linq;
using Serilog;
using Serilog.Events;
using Shouldly;

namespace HybridDb.Tests
{
    public abstract class HybridDbTests : HybridDbConfigurator, IDisposable
    {
        readonly List<string> tablesToRemove = new List<string>();
        readonly ConcurrentStack<Action> disposables;

        protected readonly ILogger logger;
        protected string connectionString;
        

        protected HybridDbTests()
        {
            logger = new LoggerConfiguration()
                .MinimumLevel.Is(Debugger.IsAttached ? LogEventLevel.Debug : LogEventLevel.Information)
                .WriteTo.ColoredConsole()
                .CreateLogger();
            
            disposables = new ConcurrentStack<Action>();

            UseTempDb();
        }

        protected virtual DocumentStore store { get; set; }

        protected static string GetConnectionString()
        {
            var isAppveyor = Environment.GetEnvironmentVariable("APPVEYOR") != null;

            return isAppveyor
                ? "Server=(local)\\SQL2014;Database=master;User ID=sa;Password=Password12!"
                : "data source =.; Integrated Security = True";
        }

        protected string Format(Table table) => store.Database.FormatTableNameAndEscape(table.Name);
        protected string Format(DocumentDesign design) => store.Database.FormatTableNameAndEscape(design.Table.Name);

        void UseTempDb()
        {
            UseTableNamePrefix(GetType().Name);

            connectionString = GetConnectionString();
            
            store = Using(new DocumentStore(configuration, connectionString, null, true));
        }

        protected void Reset()
        {
            configuration = new Configuration();

            store = Using(new DocumentStore(configuration, connectionString, store.Prefix, true));
        }

        protected T Using<T>(T disposable) where T : IDisposable
        {
            disposables.Push(disposable.Dispose);
            return disposable;
        }

        protected string NewId() => Guid.NewGuid().ToString();

        protected void Execute(SchemaMigrationCommand command)
        {
            if (command is CreateTable createTable)
            {
                tablesToRemove.Add(createTable.Table.Name);
            }

            if (command is RenameTable renameTable)
            {
                tablesToRemove.Add(renameTable.NewTableName);
            }

            command.Execute(store.Database);
        }

        protected void DropTableWhenDone(string tableName)
        {
            tablesToRemove.Add(tableName);
        }

        public void Dispose()
        {
            store.Database.RemoveTables(tablesToRemove);

            while (disposables.TryPop(out var dispose))
            {
                dispose();
            }

            Transaction.Current.ShouldBe(null);
        }

        public interface ISomeInterface
        {
            string Property { get; }
        }

        public interface IOtherInterface
        {
            string Property { get; }
        }

        public interface IUnusedInterface
        {
        }

        public class Entity : ISomeInterface
        {
            public Entity()
            {
                TheChild = new Child();
                Children = new List<Child>();
            }

            public string Id { get; set; }
            public string ProjectedProperty { get; set; }
            public List<Child> Children { get; set; }
            public string Field;
            public string Property { get; set; }
            public int Number { get; set; }
            public DateTime DateTimeProp { get; set; }
            public SomeFreakingEnum EnumProp { get; set; }
            public Child TheChild { get; set; }
            public ComplexType Complex { get; set; }

            public class Child
            {
                public string NestedProperty { get; set; }
                public double NestedDouble { get; set; }
            }

            public class ComplexType
            {
                public string A { get; set; }
                public int B { get; set; }

                public override string ToString()
                {
                    return A + B;
                }
            }
        }

        public class OtherEntity
        {
            public string Id { get; set; }
            public int Number { get; set; }
        }

        public abstract class AbstractEntity : ISomeInterface
        {
            public string Id { get; set; }
            public string Property { get; set; }
            public int Number { get; set; }
        }

        public class DerivedEntity : AbstractEntity { }
        public class MoreDerivedEntity1 : DerivedEntity, IOtherInterface { }
        public class MoreDerivedEntity2 : DerivedEntity { }

        public enum SomeFreakingEnum
        {
            One,
            Two
        }

        public class ChangeDocumentAsJObject<T> : ChangeDocument<T>
        {
            public ChangeDocumentAsJObject(Action<JObject> change)
                : base((session, serializer, json) =>
                {
                    var jObject = (JObject)serializer.Deserialize(json, typeof(JObject));
                    
                    change(jObject);
                    
                    return serializer.Serialize(jObject);
                })
            {
            }
        }
    }
}