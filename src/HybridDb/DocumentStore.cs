using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Registration;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Transactions;
using Dapper;
using HybridDb.Commands;
using HybridDb.Logging;
using HybridDb.Migration;
using HybridDb.Schema;
using System.ComponentModel.Composition;

namespace HybridDb
{
    public class DocumentStore : IDocumentStore
    {
        readonly Configuration configuration;
        readonly string connectionString;
        readonly TableMode tableMode;

        SqlConnection ambientConnectionForTesting;
        Guid lastWrittenEtag;
        long numberOfRequests;
        int numberOfManagedConnections;

        DocumentStore(string connectionString, TableMode tableMode)
        {
            this.tableMode = tableMode;
            this.connectionString = connectionString;

            configuration = new Configuration(this);

            OnMessage = message => { };
            OnRead = (table, projections) => { };
        }

        public DocumentStore(string connectionString) : this(connectionString, TableMode.UseRealTables) {}

        public Action<SqlInfoMessageEventArgs> OnMessage { get; set; }

        [ImportMany(typeof(IHybridDbExtension))]
        IEnumerable<IHybridDbExtension> AddIns { get; set; }
        public Action<Table, IDictionary<string, object>> OnRead { get; set; }

        public TableMode TableMode
        {
            get { return tableMode; }
        }

        public bool IsInTestMode
        {
            get { return tableMode != TableMode.UseRealTables; }
        }

        /// <summary>
        /// Configuration for tables and projections used in sessions.
        /// The store does not use the configuration in itself.
        /// </summary>
        public Configuration Configuration
        {
            get { return configuration; }
        }

        public DocumentDesign<TEntity> Document<TEntity>(string tablename = null)
        {
            return configuration.Document<TEntity>(tablename);
        }

        public void Migrate(Action<ISchemaMigrator> migration)
        {
            using (var migrator = new SchemaMigrator(this))
            {
                migration(migrator);
                migrator.Commit();
            }
        }

        public void MigrateSchemaToMatchConfiguration(bool safe = true)
        {
            Migrate(migrator =>
            {
                foreach (var table in Configuration.Tables.Values)
                {
                    migrator.MigrateTo(table, safe);
                }
            });
        }

        public void LoadExtensions(string path, Func<IHybridDbExtension, bool> predicate)
        {
            var registration = new RegistrationBuilder();
            registration.ForTypesDerivedFrom<IHybridDbExtension>().Export<IHybridDbExtension>();

            var container = new CompositionContainer(new DirectoryCatalog(path, registration));
            container.ComposeParts(this);

            foreach (var addIn in AddIns.Where(predicate))
            {
                RegisterExtension(addIn);
            }
        }

        public void RegisterExtension(IHybridDbExtension hybridDbExtension)
        {
            OnRead += hybridDbExtension.OnRead;
        }


        public static DocumentStore ForTestingWithGlobalTempTables(string connectionString = null)
        {
            return new DocumentStore(connectionString ?? "data source=.;Integrated Security=True", TableMode.UseGlobalTempTables);
        }

        public static DocumentStore ForTestingWithTempTables(string connectionString = null)
        {
            return new DocumentStore(connectionString ?? "data source=.;Integrated Security=True", TableMode.UseTempTables);
        }

        public IDocumentSession OpenSession()
        {
            return new DocumentSession(this);
        }

        public Guid Execute(params DatabaseCommand[] commands)
        {
            if (commands.Length == 0)
                throw new ArgumentException("No commands were passed");

            var timer = Stopwatch.StartNew();
            using (var connectionManager = Connect())
            {
                var i = 0;
                var etag = Guid.NewGuid();
                var sql = "";
                var parameters = new List<Parameter>();
                var numberOfParameters = 0;
                var expectedRowCount = 0;
                var numberOfInsertCommands = 0;
                var numberOfUpdateCommands = 0;
                var numberOfDeleteCommands = 0;
                foreach (var command in commands)
                {
                    if (command is InsertCommand)
                        numberOfInsertCommands++;

                    if (command is UpdateCommand)
                        numberOfUpdateCommands++;

                    if (command is DeleteCommand)
                        numberOfDeleteCommands++;

                    var preparedCommand = command.Prepare(this, etag, i++);
                    var numberOfNewParameters = preparedCommand.Parameters.Count;

                    if (numberOfNewParameters >= 2100)
                        throw new InvalidOperationException("Cannot execute a query with more than 2100 parameters.");

                    if (numberOfParameters + numberOfNewParameters >= 2100)
                    {
                        InternalExecute(connectionManager, sql, parameters, expectedRowCount);

                        sql = "";
                        parameters = new List<Parameter>();
                        expectedRowCount = 0;
                        numberOfParameters = 0;
                    }

                    expectedRowCount += preparedCommand.ExpectedRowCount;
                    numberOfParameters += numberOfNewParameters;

                    sql += string.Format("{0};", preparedCommand.Sql);
                    parameters.AddRange(preparedCommand.Parameters);
                }

                InternalExecute(connectionManager, sql, parameters, expectedRowCount);

                connectionManager.Complete();

                Logger.Info("Executed {0} inserts, {1} updates and {2} deletes in {3}ms",
                            numberOfInsertCommands,
                            numberOfUpdateCommands,
                            numberOfDeleteCommands,
                            timer.ElapsedMilliseconds);

                lastWrittenEtag = etag;
                return etag;
            }
        }

        void InternalExecute(ManagedConnection managedConnection, string sql, List<Parameter> parameters, int expectedRowCount)
        {
            var fastParameters = new FastDynamicParameters(parameters);
            var rowcount = managedConnection.Connection.Execute(sql, fastParameters);
            Interlocked.Increment(ref numberOfRequests);
            if (rowcount != expectedRowCount)
                throw new ConcurrencyException();
        }

        public Guid Insert(Table table, Guid key, object projections)
        {
            return Execute(new InsertCommand(table, key, projections));
        }

        public Guid Update(Table table, Guid key, Guid etag, object projections, bool lastWriteWins = false)
        {
            return Execute(new UpdateCommand(table, key, etag, projections, lastWriteWins));
        }

        public void Delete(Table table, Guid key, Guid etag, bool lastWriteWins = false)
        {
            Execute(new DeleteCommand(table, key, etag, lastWriteWins));
        }

        public IEnumerable<TProjection> Query<TProjection>(Table table, out QueryStats stats, string select = null, string where = "",
                                                           int skip = 0, int take = 0, string orderby = "", object parameters = null)
        {
            if (select.IsNullOrEmpty() || select == "*")
                select = "";

            var projectToDictionary = typeof (TProjection).IsA<IDictionary<string, object>>();
            if (!projectToDictionary)
                select = MatchSelectedColumnsWithProjectedType<TProjection>(select);

            var timer = Stopwatch.StartNew();
            using (var connection = Connect())
            {
                var sql = new SqlBuilder();

                sql.Append("select count(*) as TotalResults")
                   .Append("from {0}", FormatTableNameAndEscape(table.Name))
                   .Append(!string.IsNullOrEmpty(@where), "where {0}", @where)
                   .Append(";");

                var isWindowed = skip > 0 || take > 0;

                if (isWindowed)
                {
                    sql.Append(@"with temp as (select *")
                       .Append(", row_number() over(ORDER BY {0}) as RowNumber", string.IsNullOrEmpty(@orderby) ? "CURRENT_TIMESTAMP" : @orderby)
                       .Append("from {0}", FormatTableNameAndEscape(table.Name))
                       .Append(!string.IsNullOrEmpty(@where), "where {0}", @where)
                       .Append(")")
                       .Append("select {0} from temp", select.IsNullOrEmpty() ? "*" : select + ", RowNumber")
                       .Append("where RowNumber >= {0}", skip + 1)
                       .Append(take > 0, "and RowNumber <= {0}", skip + take)
                       .Append("order by RowNumber");
                }
                else
                {
                    sql.Append(@"with temp as (select *")
                       .Append(", 0 as RowNumber")
                       .Append("from {0}", FormatTableNameAndEscape(table.Name))
                       .Append(!string.IsNullOrEmpty(@where), "where {0}", @where)
                       .Append(")")
                       .Append("select {0} from temp", select.IsNullOrEmpty() ? "*" : select + ", RowNumber")
                       .Append(!string.IsNullOrEmpty(orderby), "order by {0}", orderby);
                }
                
                IEnumerable<TProjection> result;
                if (projectToDictionary)
                {
                    result = (IEnumerable<TProjection>)
                             InternalQuery<object>(connection, sql, parameters, out stats)
                                 .Cast<IDictionary<string, object>>()
                                 .Select(row =>
                                 {
                                     OnRead(table, row);
                                     return row;
                                 });
                }
                else
                {
                    result = InternalQuery<TProjection>(connection, sql, parameters, out stats);
                }

                stats.QueryDurationInMilliseconds = timer.ElapsedMilliseconds;

                if (isWindowed)
                {
                    var potential = stats.TotalResults - skip;
                    if (potential < 0)
                        potential = 0;

                    stats.RetrievedResults = take > 0 && potential > take ? take : potential;
                }
                else
                {
                    stats.RetrievedResults = stats.TotalResults;
                }

                Interlocked.Increment(ref numberOfRequests);

                Logger.Info("Retrieved {0} of {1} in {2}ms", stats.RetrievedResults, stats.TotalResults, stats.QueryDurationInMilliseconds);

                connection.Complete();
                return result;
            }
        }

        public IEnumerable<IDictionary<string, object>> Query(Table table, out QueryStats stats, string select = null, string where = "",
                                                              int skip = 0, int take = 0, string orderby = "", object parameters = null)
        {
            return Query<IDictionary<string, object>>(table, out stats, select, @where, skip, take, orderby, parameters);
        }


        static string MatchSelectedColumnsWithProjectedType<TProjection>(string select)
        {
            var neededColumns = typeof(TProjection).GetProperties().Select(x => x.Name).ToList();
            var selectedColumns = from clause in @select.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                  let split = Regex.Split(clause, " AS ", RegexOptions.IgnoreCase).Where(x => x != "").ToArray()
                                  let column = split[0]
                                  let alias = split.Length > 1 ? split[1] : null
                                  where neededColumns.Contains(alias)
                                  select new { column, alias = alias ?? column };

            var missingColumns = from column in neededColumns
                                 where !selectedColumns.Select(x => x.alias).Contains(column)
                                 select new { column, alias = column };

            select = string.Join(", ", selectedColumns.Union(missingColumns).Select(x => x.column + " AS " + x.alias));
            return select;
        }

        IEnumerable<T> InternalQuery<T>(ManagedConnection connection, SqlBuilder sql, object parameters, out QueryStats stats)
        {
            var normalizedParameters = new FastDynamicParameters(
                parameters as IEnumerable<Parameter> ?? ConvertToParameters<T>(parameters));

            IEnumerable<T> rows;
            using (var reader = connection.Connection.QueryMultiple(sql.ToString(), normalizedParameters))
            {
                stats = reader.Read<QueryStats>(buffered: true).Single();
                rows = reader.Read<T, object, T>((first, second) => first, "RowNumber", buffered: true);

                Interlocked.Increment(ref numberOfRequests);

            }
            rows.ToList();

            return rows;
        }

        static IEnumerable<Parameter> ConvertToParameters<T>(object parameters)
        {
            return from projection in parameters as IDictionary<string, object> ?? ObjectToDictionaryRegistry.Convert(parameters)
                   select new Parameter { Name = "@" + projection.Key, Value = projection.Value };
        }

        public IDictionary<string, object> Get(Table table, Guid key)
        {
            var timer = Stopwatch.StartNew();
            using (var connection = Connect())
            {
                string sql;
                var indexTable = table as IndexTable;
                if (indexTable != null)
                {
                    sql = string.Format("declare @table varchar(255) = (select {0} from {1} where {2} = @Id);" +
                                        "if @table is null select null where 1<>1;" +
	                                    "declare @sql varchar(255) = 'select *, ' + quotename(@table, '''') + ' as TableReference from ' + quotename('{3}' + @table) + ' where {2} = ' + quotename(cast(@Id as varchar(36)), '''');" +
                                        "exec(@sql);",
                                        indexTable.TableReferenceColumn.Name,
                                        FormatTableNameAndEscape(indexTable.Name),
                                        indexTable.IdColumn.Name,
                                        GetTablePrefix());
                }
                else
                {
                    sql = string.Format("select * from {0} where {1} = @Id",
                                        FormatTableNameAndEscape(table.Name),
                                        table.IdColumn.Name);
                }


                var row = ((IDictionary<string, object>)connection.Connection.Query(sql, new { Id = key }).SingleOrDefault());

                Interlocked.Increment(ref numberOfRequests);

                Logger.Info("Retrieved {0} in {1}ms", key, timer.ElapsedMilliseconds);

                connection.Complete();

                if (row == null)
                    return null;

                OnRead(table, row);

                return row;
            }
        }

        public long NumberOfRequests
        {
            get { return numberOfRequests; }
        }

        public Guid LastWrittenEtag
        {
            get { return lastWrittenEtag; }
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

        static Dictionary<Column, object> ConvertToColumnValueDictionary(Table table, IDictionary<string, object> row)
        {
            return (from value in row
                    let column = table[value]
                    where column != null
                    select new KeyValuePair<Column, object>(column, value.Value))
                .ToDictionary();
        }

        ManagedConnection Connect()
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
                if (IsInTestMode)
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

        public void Dispose()
        {
            if (numberOfManagedConnections > 0)
                Logger.Warn("A ManagedConnection was not properly disposed. You may be leaking sql connections or transactions.");

            if (ambientConnectionForTesting != null)
                ambientConnectionForTesting.Dispose();
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


        ILogger Logger
        {
            get { return Configuration.Logger; }
        }

        public class MissingProjectionValueException : Exception {}
    }

    public enum TableMode
    {
        UseRealTables,
        UseTempTables,
        UseGlobalTempTables
    }
}