using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using HybridDb.Commands;
using HybridDb.Config;
using Newtonsoft.Json;

namespace HybridDb.Events.Commands
{
    public class AppendEvent : Command<EventData<byte[]>>
    {
        public AppendEvent(Table table, Generation generation, EventData<byte[]> @event)
        {
            Table = table;
            Generation = generation;
            Event = @event;
        }

        public Table Table { get; }
        public Generation Generation { get; }
        public EventData<byte[]> Event { get; }

        public static EventData<byte[]> Execute(DocumentTransaction tx, AppendEvent command)
        {
            var parameters = new DynamicParameters();

            if (command.Event.SequenceNumber == SequenceNumber.Any)
                throw new InvalidOperationException("SequenceNumber.Any is not currently supported.");

            if (command.Event.SequenceNumber < SequenceNumber.BeginningOfStream)
                throw new InvalidOperationException("SequenceNumber must be 0 or a positive integer.");

            if (string.IsNullOrWhiteSpace(command.Event.StreamId))
                throw new InvalidOperationException("StreamId must be set.");

            parameters.Add("@EventId", command.Event.EventId);
            parameters.Add("@CommitId", tx.CommitId);
            parameters.Add("@StreamId", command.Event.StreamId);
            parameters.Add("@SequenceNumber", command.Event.SequenceNumber);
            parameters.Add("@Name", command.Event.Name);
            parameters.Add("@Generation", command.Generation.ToString());
            parameters.Add("@Metadata", JsonConvert.SerializeObject(command.Event.Metadata.Values));
            parameters.Add("@Data", command.Event.Data);

            var sql = $@"
                INSERT INTO {tx.Store.Database.FormatTableNameAndEscape(command.Table.Name)} (EventId, CommitId, StreamId, SequenceNumber, Name, Generation, Metadata, Data) 
                OUTPUT Inserted.Position, Inserted.SequenceNumber
                VALUES (@EventId, @CommitId, @StreamId, @SequenceNumber, @Name, @Generation, @Metadata, @Data)";

            try
            {
                var (position, seq) = tx.SqlConnection.Query<long, long, (long globSeq, long seq)>(sql, ValueTuple.Create, parameters, tx.SqlTransaction, splitOn: "*").First();

                return command.Event.WithSeq(seq);
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