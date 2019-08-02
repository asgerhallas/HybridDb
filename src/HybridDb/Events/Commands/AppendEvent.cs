using System;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using HybridDb.Commands;
using HybridDb.Config;
using Newtonsoft.Json;

namespace HybridDb.Events.Commands
{
    public class AppendEvent : Command<(long Position, EventData<byte[]> Event)>
    {
        public AppendEvent(Table table, int generation, EventData<byte[]> @event)
        {
            Table = table;
            Generation = generation;
            Event = @event;
        }

        public Table Table { get; }
        public int Generation { get; }
        public EventData<byte[]> Event { get; }

        public static (long, EventData<byte[]>) Execute(DocumentTransaction tx, AppendEvent command)
        {
            if (command.Event.SequenceNumber < SequenceNumber.Any)
                throw new InvalidOperationException("SequenceNumber must be SequenceNumber.Any, SequenceNumber.BeginningOfStream or a positive integer.");

            if (string.IsNullOrWhiteSpace(command.Event.StreamId))
                throw new InvalidOperationException("StreamId must be set.");

            var parameters = new DynamicParameters();

            parameters.Add("@CommitId", tx.CommitId);
            parameters.Add("@EventId", command.Event.EventId);
            parameters.Add("@StreamId", command.Event.StreamId);
            parameters.Add("@SequenceNumber", command.Event.SequenceNumber);
            parameters.Add("@Name", command.Event.Name);
            parameters.Add("@Generation", command.Generation.ToString());
            parameters.Add("@Metadata", JsonConvert.SerializeObject(command.Event.Metadata.Values));
            parameters.Add("@Data", command.Event.Data);

            var tablename = tx.Store.Database.FormatTableNameAndEscape(command.Table.Name);

            var sequenceNumberSql = command.Event.SequenceNumber == SequenceNumber.Any
                ? $"(SELECT COALESCE(MAX(SequenceNumber), -1) + 1 FROM {tablename} WHERE StreamId = @StreamId)"
                : "@SequenceNumber";

            var sql = $@"
                INSERT INTO {tablename} (EventId, CommitId, StreamId, SequenceNumber, Name, Generation, Metadata, Data) 
                OUTPUT Inserted.Position, Inserted.SequenceNumber
                VALUES (@EventId, @CommitId, @StreamId, {sequenceNumberSql}, @Name, @Generation, @Metadata, @Data)";

            try
            {
                var (position, sequenceNumber) = tx.SqlConnection.Query<long, long, (long position, long sequenceNumber)>(sql, ValueTuple.Create, parameters, tx.SqlTransaction, splitOn: "*").First();

                return (position, command.Event.WithSeq(sequenceNumber));
            }
            catch (SqlException sqlException)
            {
                if (sqlException.Errors.OfType<SqlError>().Any(x => x.Number == 2601))
                {
                    throw new ConcurrencyException($"Event {command.Event.StreamId}/{command.Event.SequenceNumber} already exists.", sqlException);
                }

                throw;
            }
        }
    }
}