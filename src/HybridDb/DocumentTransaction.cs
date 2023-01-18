using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Transactions;
using Dapper;
using HybridDb.Commands;
using HybridDb.Config;
using IsolationLevel = System.Data.IsolationLevel;

namespace HybridDb
{
    public class DocumentTransaction : IDisposable
    {
        static readonly HashSet<Type> simpleTypes = new()
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

        readonly ManagedConnection managedConnection;
        readonly StoreStats storeStats;

        public DocumentTransaction(DocumentStore store, Guid commitId, IsolationLevel level, StoreStats storeStats)
        {
            Store = store;

            this.storeStats = storeStats;

            managedConnection = store.Database.Connect();
            SqlConnection = managedConnection.Connection;

            if (Transaction.Current == null)
            {
                SqlTransaction = SqlConnection.BeginTransaction(level);

                Counter.TransactionCreated();
            }

            CommitId = commitId;
        }

        public Guid CommitId { get; }

        public IDocumentStore Store { get; }
        public SqlConnection SqlConnection { get; }
        public SqlTransaction SqlTransaction { get; }

        public void Dispose()
        {
            SqlTransaction?.Dispose();
            Counter.TransactionDisposed();
            managedConnection.Dispose();
        }

        public Guid Complete()
        {
            SqlTransaction?.Commit();
            managedConnection.Complete();
            return CommitId;
        }

        public T Execute<T>(Command<T> command)
        {
            return Store.Execute(this, command);
        }

        public IDictionary<string, object> Get(DocumentTable table, string key)
        {
            var result = Execute(new GetCommand(table, new List<string> { key }));

            if (result.Count == 0) return null;

            return result.Values.Single();
        }

        public IDictionary<string, IDictionary<string, object>> Get(DocumentTable table, IReadOnlyList<string> keys)
        {
            return Execute(new GetCommand(table, keys));
        }

        public (QueryStats stats, IEnumerable<QueryResult<TProjection>> rows) Query<TProjection>(
            DocumentTable table, string join, bool top1 = false, string select = null, string where = "",
            Window window = null, string orderby = "", bool includeDeleted = false, object parameters = null)
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

            switch (Store.Configuration.SoftDelete)
            {
                case false when includeDeleted:
                    throw new InvalidOperationException("Soft delete is not enabled, please configure with UseSoftDelete.");
                case true when !includeDeleted:
                    where = string.IsNullOrEmpty(where)
                        ? $"{DocumentTable.LastOperationColumn.Name} <> {Operation.Deleted:D}" // TODO: Use parameters for performance
                        : $"({where}) AND ({DocumentTable.LastOperationColumn.Name} <> {Operation.Deleted:D})";
                    break;
            }

            QueryStats stats = null;
            IEnumerable<QueryResult<TProjection>> result = null;
            var timer = Stopwatch.StartNew();
            var sqlx = new SqlBuilder();
            var isWindowed = window != null;

            var from = Store.Database.FormatTableNameAndEscape(table.Name);

            if (isWindowed || top1)
            {
                sqlx.Append("select count(*) as TotalResults")
                    .Append($"from {from}")
                    .Append(!string.IsNullOrEmpty(join), join)
                    .Append(!string.IsNullOrEmpty(where), $"where {where}")
                    .Append(";");

                sqlx.Append(string.IsNullOrEmpty(select), @"with WithRowNumber as (select *", $@"with WithRowNumber as (select {select}")
                    .Append($", row_number() over(ORDER BY {(string.IsNullOrEmpty(orderby) ? "CURRENT_TIMESTAMP" : orderby)}) - 1 as RowNumber")
                    .Append($", {from}.{DocumentTable.DiscriminatorColumn.Name} as __Discriminator")
                    .Append($", {from}.{DocumentTable.LastOperationColumn.Name} as __LastOperation")
                    .Append($", {from}.{DocumentTable.TimestampColumn.Name} as __RowVersion")
                    .Append($"from {from}")
                    .Append(!string.IsNullOrEmpty(join), join)
                    .Append(!string.IsNullOrEmpty(where), $"where {where}")
                    .Append(")")
                    .Append(top1, "select top 1", "select")
                    .Append("*")
                    .Append("from WithRowNumber");

                switch (window)
                {
                    case SkipTake skipTake:
                    {
                        var skip = skipTake.Skip;
                        var take = skipTake.Take;

                        sqlx.Append("where RowNumber >= @skip", new SqlParameter("skip", skip))
                            .Append(take > 0, "and RowNumber < @take", new SqlParameter("take", skip + take))
                            .Append("order by RowNumber");
                        break;
                    }
                    case SkipToId skipToId:
                        sqlx.Append(
                                "where RowNumber >= (select top 1 * from (select RowNumber - (RowNumber % @__PageSize) as FirstRow from WithRowNumber where Id=@__Id union all select 0 as FirstRow) as x order by FirstRow desc)")
                            .Append(
                                "and RowNumber < (select top 1 * from (select RowNumber - (RowNumber % @__PageSize) as FirstRow from WithRowNumber where Id=@__Id union all select 0 as FirstRow) as x order by FirstRow desc) + @__PageSize")
                            .Append("order by RowNumber", new SqlParameter("__Id", skipToId.Id), new SqlParameter("__PageSize", skipToId.PageSize));
                        break;
                    case null: break;
                }

                var internalResult = InternalQuery(sqlx, parameters, reader => new
                {
                    Stats = reader.Read<QueryStats>(true).Single(),
                    Rows = ReadRow<TProjection>(reader)
                });

                result = internalResult.Rows.Select(x => new QueryResult<TProjection>(x.Data, x.Discriminator, x.LastOperation, x.RowVersion));

                stats = new QueryStats
                {
                    TotalResults = internalResult.Stats.TotalResults,
                    RetrievedResults = internalResult.Rows.Count(),
                    FirstRowNumberOfWindow = internalResult.Rows.FirstOrDefault()?.RowNumber ?? 0
                };
            }
            else
            {
                sqlx.Append(string.IsNullOrEmpty(select), "select *", $"select {select}")
                    .Append(", 0 as RowNumber")
                    .Append($", {from}.{DocumentTable.DiscriminatorColumn.Name} as __Discriminator")
                    .Append($", {from}.{DocumentTable.LastOperationColumn.Name} AS __LastOperation")
                    .Append($", {from}.{DocumentTable.TimestampColumn.Name} AS __RowVersion")
                    .Append($"from {from}")
                    .Append(!string.IsNullOrEmpty(join), join)
                    .Append(!string.IsNullOrEmpty(where), $"where ({where})")
                    .Append(!string.IsNullOrEmpty(orderby), $"order by {orderby}");

                result = InternalQuery(sqlx, parameters, ReadRow<TProjection>)
                    .Select(x => new QueryResult<TProjection>(x.Data, x.Discriminator, x.LastOperation, x.RowVersion))
                    .ToList();

                stats = new QueryStats();
                stats.TotalResults = stats.RetrievedResults = result.Count();
                stats.FirstRowNumberOfWindow = 0;
            }

            stats.QueryDurationInMilliseconds = timer.ElapsedMilliseconds;

            return (stats, result);
        }

        static string MatchSelectedColumnsWithProjectedType<TProjection>(string select)
        {
            if (simpleTypes.Contains(typeof(TProjection)))
            {
                return select;
            }

            var neededColumns = typeof(TProjection).GetProperties().Select(x => x.Name).ToList();
            var selectedColumns =
                from clause in @select.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                let split = Regex.Split(clause, " AS ", RegexOptions.IgnoreCase).Where(x => x != "").ToArray()
                let column = split[0]
                let alias = split.Length > 1 ? split[1] : null
                where neededColumns.Contains(alias)
                select new { column, alias = alias ?? column };

            var missingColumns =
                from column in neededColumns
                where !selectedColumns.Select(x => x.alias).Contains(column)
                select new { column, alias = column };

            select = string.Join(", ", selectedColumns.Union(missingColumns).Select(x => x.column + " AS " + x.alias));
            return select;
        }

        T InternalQuery<T>(SqlBuilder sql, object parameters, Func<SqlMapper.GridReader, T> read)
        {
            var hybridDbParameters = parameters.ToHybridDbParameters();

            hybridDbParameters.Add(sql.Parameters);

            using var reader = SqlConnection.QueryMultiple(sql.ToString(), hybridDbParameters, SqlTransaction);

            return read(reader);
        }

        public IEnumerable<Row<T>> ReadRow<T>(SqlMapper.GridReader reader)
        {
            if (typeof(IDictionary<string, object>).IsAssignableFrom(typeof(T)))
            {
                return (IEnumerable<Row<T>>)reader
                    .Read<object, RowExtras, Row<IDictionary<string, object>>>(
                        (a, b) => CreateRow((IDictionary<string, object>)a, b),
                        "RowNumber",
                        true);
            }

            return reader.Read<T, RowExtras, Row<T>>(CreateRow, "RowNumber", true);
        }

        public static Row<T> CreateRow<T>(T data, RowExtras extras)
        {
            return new(data, extras);
        }

        public class RowExtras
        {
            public int RowNumber { get; set; }
            public string __Discriminator { get; set; }
            public Operation __LastOperation { get; set; }
            public byte[] __RowVersion { get; set; }
        }

        public class Row<T>
        {
            public Row(T data, RowExtras extras)
            {
                Data = data;
                RowNumber = extras.RowNumber;
                Discriminator = extras.__Discriminator;
                LastOperation = extras.__LastOperation;
                RowVersion = extras.__RowVersion;
            }

            public T Data { get; set; }
            public int RowNumber { get; set; }
            public string Discriminator { get; set; }
            public Operation LastOperation { get; set; }
            public byte[] RowVersion { get; set; }
        }
    }

    public class SqlDatabaseCommand
    {
        public string Sql { get; set; }
        public HybridDbParameters Parameters { get; set; }
        public int ExpectedRowCount { get; set; }
    }
}