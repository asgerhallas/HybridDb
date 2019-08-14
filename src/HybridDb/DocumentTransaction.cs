using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Transactions;
using Dapper;
using HybridDb.Commands;
using HybridDb.Config;
using ShinySwitch;
using IsolationLevel = System.Data.IsolationLevel;

namespace HybridDb
{
    public class DocumentTransaction : IDisposable
    {
        readonly StoreStats storeStats;
        readonly ManagedConnection managedConnection;

        public DocumentTransaction(DocumentStore store, Guid commitId, IsolationLevel level, StoreStats storeStats)
        {
            Store = store;

            this.storeStats = storeStats;

            managedConnection = store.Database.Connect();
            SqlConnection = managedConnection.Connection;

            if (Transaction.Current == null)
            {
                SqlTransaction = SqlConnection.BeginTransaction(level);
            }

            CommitId = commitId;
        }

        public void Dispose()
        {
            SqlTransaction?.Dispose();
            managedConnection.Dispose();
        }

        public Guid Complete()
        {
            SqlTransaction?.Commit();
            return CommitId;
        }

        public Guid CommitId { get; }

        public DocumentStore Store { get; }
        public SqlConnection SqlConnection { get; }
        public SqlTransaction SqlTransaction { get; }

        public T Execute<T>(Command<T> command) => Store.Execute(this, command);

        public IDictionary<string, object> Get(DocumentTable table, string key)
        {
            storeStats.NumberOfRequests++;
            storeStats.NumberOfGets++;

            var sql = $"select * from {Store.Database.FormatTableNameAndEscape(table.Name)} where {DocumentTable.IdColumn.Name} = @Id and {DocumentTable.LastOperationColumn.Name} <> @Op";

            return (IDictionary<string, object>) SqlConnection.Query(sql, new {Id = key, Op = Operation.Deleted}, SqlTransaction).SingleOrDefault();
        }

        public (QueryStats stats, IEnumerable<QueryResult<TProjection>> rows) Query<TProjection>(
            DocumentTable table, string select = null, string where = "", int skip = 0, int take = 0,
            string orderby = "", bool includeDeleted = false, object parameters = null)
        {
            storeStats.NumberOfRequests++;
            storeStats.NumberOfQueries++;

            if (string.IsNullOrEmpty(select) || select == "*")
            {
                select = "";

                if (typeof(TProjection) != typeof(object))
                {
                    select = MatchSelectedColumnsWithProjectedType<TProjection>(select);
                }
            }

            if (!includeDeleted)
            {
                where = string.IsNullOrEmpty(where)
                    ? $"{DocumentTable.LastOperationColumn.Name} <> {Operation.Deleted:D}" // TODO: Use parameters for performance
                    : $"({where}) AND ({DocumentTable.LastOperationColumn.Name} <> {Operation.Deleted:D})";
            }

            var timer = Stopwatch.StartNew();

            var sql = new SqlBuilder();

            var isWindowed = skip > 0 || take > 0;

            if (isWindowed)
            {
                sql.Append("select count(*) as TotalResults")
                    .Append($"from {Store.Database.FormatTableNameAndEscape(table.Name)}")
                    .Append(!string.IsNullOrEmpty(where), $"where {where}")
                    .Append(";");

                sql.Append(@"with temp as (select *")
                    .Append($", {DocumentTable.DiscriminatorColumn.Name} as __Discriminator")
                    .Append($", {DocumentTable.LastOperationColumn.Name} as __LastOperation")
                    .Append($", {DocumentTable.TimestampColumn.Name} as __RowVersion")
                    .Append($", row_number() over(ORDER BY {(string.IsNullOrEmpty(orderby) ? "CURRENT_TIMESTAMP" : orderby)}) as RowNumber")
                    .Append($"from {Store.Database.FormatTableNameAndEscape(table.Name)}")
                    .Append(!string.IsNullOrEmpty(where), $"where {where}")
                    .Append(")")
                    .Append(string.IsNullOrEmpty(select), "select * from temp", $"select {select}, __Discriminator, __LastOperation, __RowVersion from temp")
                    .Append($"where RowNumber >= {skip + 1}")
                    .Append(take > 0, $"and RowNumber <= {skip + take}")
                    .Append("order by RowNumber")
                    .Append(";");
            }
            else
            {
                sql.Append(string.IsNullOrEmpty(select), "select *", $"select {select}")
                    .Append($", {DocumentTable.DiscriminatorColumn.Name} as __Discriminator")
                    .Append($", {DocumentTable.LastOperationColumn.Name} AS __LastOperation")
                    .Append($", {DocumentTable.TimestampColumn.Name} AS __RowVersion")
                    .Append($"from {Store.Database.FormatTableNameAndEscape(table.Name)}")
                    .Append(!string.IsNullOrEmpty(where), $"where ({where})")
                    .Append(!string.IsNullOrEmpty(orderby), $"order by {orderby}");
            }

            var result = InternalQuery<TProjection>(sql, parameters, isWindowed, out var stats);

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

            return (stats, result);
        }

        static string MatchSelectedColumnsWithProjectedType<TProjection>(string select)
        {
            if (simpleTypes.Contains(typeof(TProjection)))
                return select;

            var neededColumns = typeof(TProjection).GetProperties().Select(x => x.Name).ToList();
            var selectedColumns =
                from clause in @select.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)
                let split = Regex.Split(clause, " AS ", RegexOptions.IgnoreCase).Where(x => x != "").ToArray()
                let column = split[0]
                let alias = split.Length > 1 ? split[1] : null
                where neededColumns.Contains(alias)
                select new {column, alias = alias ?? column};

            var missingColumns =
                from column in neededColumns
                where !selectedColumns.Select(x => x.alias).Contains(column)
                select new {column, alias = column};

            select = string.Join(", ", selectedColumns.Union(missingColumns).Select(x => x.column + " AS " + x.alias));
            return select;
        }

        IEnumerable<QueryResult<T>> InternalQuery<T>(SqlBuilder sql, object parameters, bool hasTotalsQuery, out QueryStats stats)
        {
            var normalizedParameters = parameters as Parameters ?? Parameters.FromAnonymousObject(parameters);

            if (hasTotalsQuery)
            {
                using (var reader = SqlConnection.QueryMultiple(sql.ToString(), normalizedParameters, SqlTransaction))
                {
                    stats = reader.Read<QueryStats>(buffered: true).Single();

                    return reader.Read<T, string, Operation, byte[], QueryResult<T>>((obj, a, b, c) =>
                        new QueryResult<T>(obj, a, b, c), splitOn: "__Discriminator,__LastOperation,__RowVersion", buffered: true);
                }
            }

            using (var reader = SqlConnection.QueryMultiple(sql.ToString(), normalizedParameters, SqlTransaction))
            {
                var rows = (List<QueryResult<T>>) reader.Read<T, string, Operation, byte[], QueryResult<T>>((obj, a, b, c) =>
                    new QueryResult<T>(obj, a, b, c), splitOn: "__Discriminator,__LastOperation,__RowVersion", buffered: true);

                stats = new QueryStats
                {
                    TotalResults = rows.Count
                };

                return rows;
            }
        }

        static readonly HashSet<Type> simpleTypes = new HashSet<Type>
        {
            typeof(byte),
            typeof(sbyte),
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            typeof(float),
            typeof(double),
            typeof(decimal),
            typeof(bool),
            typeof(string),
            typeof(char),
            typeof(Guid),
            typeof(DateTime),
            typeof(DateTimeOffset),
            typeof(TimeSpan),
            typeof(byte[]),
            typeof(byte?),
            typeof(sbyte?),
            typeof(short?),
            typeof(ushort?),
            typeof(int?),
            typeof(uint?),
            typeof(long?),
            typeof(ulong?),
            typeof(float?),
            typeof(double?),
            typeof(decimal?),
            typeof(bool?),
            typeof(char?),
            typeof(Guid?),
            typeof(DateTime?),
            typeof(DateTimeOffset?),
            typeof(TimeSpan?),
            typeof(object)
        };
    }

    public class SqlDatabaseCommand
    {
        public string Sql { get; set; }
        public Parameters Parameters { get; set; }
        public int ExpectedRowCount { get; set; }
    }
}