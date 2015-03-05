using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Dapper;
using HybridDb.Logging;
using HybridDb.Migration;
using HybridDb.Migration.Commands;
using HybridDb.Schema;

namespace HybridDb
{
    public class DocumentStoreMigrator
    {
        public Task Migrate(IDocumentStore store)
        {
            store.Migrate(migrator =>
            {
                foreach (var table in store.Configuration.Tables.Values)
                {
                    migrator.MigrateTo(table, true);
                }
            });
            return Task.FromResult(1);
        }

        public IReadOnlyList<SchemaMigrationCommand> FindSchemaChanges(IDatabase db, Configuration configuration)
        {
            var commands = new List<SchemaMigrationCommand>();

            foreach (var design in configuration.DocumentDesigns)
            {
                if (commands.OfType<CreateTable>().Any(x => x.Table == design.Table) ||
                    db.TableExists(design.Table.Name))
                {
                    continue;
                }

                commands.Add(new CreateTable(design.Table));
            }

            var tables = db.GetTables();
            foreach (var tablename in tables)
            {
                if (configuration.Tables.ContainsKey(tablename))
                    continue;

                commands.Add(new RemoveTable(tablename));
            }

            return commands;
        }
    }

    public interface IDatabase
    {
        bool TableExists(string name);
        List<string> GetTables();
    }

    public class Database : IDatabase
    {
        readonly string connectionString;
        readonly TableMode tableMode;
        readonly bool isInTestMode;

        int numberOfManagedConnections;
        SqlConnection ambientConnectionForTesting;

        public Database(string connectionString, TableMode tableMode, bool isInTestMode)
        {
            this.connectionString = connectionString;
            this.tableMode = tableMode;
            this.isInTestMode = isInTestMode;

            OnMessage = message => { };
        }

        public Action<SqlInfoMessageEventArgs> OnMessage { get; set; }
        public ILogger Logger { get; set; }

        public TableMode TableMode
        {
            get { return tableMode; }
        }

        public string FormatTableNameAndEscape(string tablename)
        {
            return Escape(FormatTableName(tablename));
        }

        public string Escape(string identifier)
        {
            return string.Format("[{0}]", identifier);
        }

        public string FormatTableName(string tablename)
        {
            return GetTablePrefix() + tablename;
        }

        public string GetTablePrefix()
        {
            switch (tableMode)
            {
                case TableMode.UseRealTables:
                    return "";
                case TableMode.UseTempTables:
                    return "#";
                case TableMode.UseGlobalTempTables:
                    return "##";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public ManagedConnection Connect()
        {
            Action complete = () => { };
            Action dispose = () => { numberOfManagedConnections--; };

            try
            {
                if (Transaction.Current == null)
                {
                    var tx = new TransactionScope();
                    complete += tx.Complete;
                    dispose += tx.Dispose;
                }

                SqlConnection connection;
                if (isInTestMode)
                {
                    // We don't care about thread safety in test mode
                    if (ambientConnectionForTesting == null)
                    {
                        ambientConnectionForTesting = new SqlConnection(connectionString);
                        ambientConnectionForTesting.InfoMessage += (obj, args) => OnMessage(args);
                        ambientConnectionForTesting.Open();

                    }

                    connection = ambientConnectionForTesting;
                }
                else
                {
                    connection = new SqlConnection(connectionString);
                    connection.InfoMessage += (obj, args) => OnMessage(args);
                    connection.Open();

                    complete = connection.Dispose + complete;
                    dispose = connection.Dispose + dispose;
                }

                // Connections that are kept open during multiple operations (for testing mostly)
                // will not automatically be enlisted in transactions started later, we fix that here.
                // Calling EnlistTransaction on a connection that is already enlisted is a no-op.
                connection.EnlistTransaction(Transaction.Current);

                numberOfManagedConnections++;

                return new ManagedConnection(connection, complete, dispose);
            }
            catch (Exception)
            {
                dispose();
                throw;
            }
        }

        public void RawExecute(string sql, object parameters = null)
        {
            var hdbParams = parameters as IEnumerable<Parameter>;
            if (hdbParams != null)
                parameters = new FastDynamicParameters(hdbParams);

            using (var connection = Connect())
            {
                connection.Connection.Execute(sql, parameters);
                connection.Complete();
            }
        }

        public IEnumerable<T> RawQuery<T>(string sql, object parameters = null)
        {
            var hdbParams = parameters as IEnumerable<Parameter>;
            if (hdbParams != null)
                parameters = new FastDynamicParameters(hdbParams);

            using (var connection = Connect())
            {
                return connection.Connection.Query<T>(sql, parameters);
            }
        }

        public bool TableExists(string name)
        {
            return tableMode == TableMode.UseRealTables
                ? RawQuery<dynamic>(string.Format("select OBJECT_ID('{0}') as Result", name)).First().Result != null
                : RawQuery<dynamic>(string.Format("select OBJECT_ID('tempdb..{0}') as Result", FormatTableName(name))).First().Result != null;
        }

        public List<string> GetTables()
        {
            return tableMode == TableMode.UseRealTables
                ? RawQuery<string>("select table_name from information_schema.tables where table_type='BASE TABLE'").ToList()
                : RawQuery<string>("select * from tempdb.sys.objects where object_id('tempdb.dbo.' + name, 'U') is not null AND name LIKE '#%'")
                    .ToList();
        }

        public Column GetColumn(string table, string column)
        {
            return tableMode == TableMode.UseRealTables
                ? RawQuery<Column>(string.Format("select * from sys.columns where Name = N'{0}' and Object_ID = Object_ID(N'{1}')", column, table)).FirstOrDefault()
                : RawQuery<Column>(string.Format("select * from tempdb.sys.columns where Name = N'{0}' and Object_ID = Object_ID(N'tempdb..{1}')", column, FormatTableName(table))).FirstOrDefault();
        }

        public string GetType(int id)
        {
            var rawQuery = RawQuery<string>("select name from sys.types where system_type_id = @id", new { id });
            return rawQuery.FirstOrDefault();
        }

        public void Dispose()
        {
            if (numberOfManagedConnections > 0)
                Logger.Warn("A ManagedConnection was not properly disposed. You may be leaking sql connections or transactions.");

            if (ambientConnectionForTesting != null)
                ambientConnectionForTesting.Dispose();
        }

        public class Column
        {
            public string Name { get; set; }
            public int system_type_id { get; set; }
            public int max_length { get; set; }
        }
    }

    //public interface IMigration
    //{
    //    //void InitializeDatabase();

    //    IMigrator CreateMigrator();

    //    void AddTable<TEntity>();
    //    void RemoveTable(string tableName);
    //    void RenameTable(string oldTableName, string newTableName);
        
    //    void UpdateProjectionFor<TEntity, TMember>(Expression<Func<TEntity, TMember>> member);
        
    //    void AddProjection<TEntity, TMember>(Expression<Func<TEntity, TMember>> member);
    //    void RemoveProjection<TEntity>(string columnName);
    //    void RenameColumn<TEntity>(string oldColumnName, string newColumnName);
        
    //    void Do<T>(string tableName, Action<T, IDictionary<string, object>> action);

    //    //void Execute(string sql);
    //}
}