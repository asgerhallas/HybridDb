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
    public class AppendEventCommand : Command<EventData<byte[]>>
    {
        public AppendEventCommand(Table table, Generation generation, EventData<byte[]> @event)
        {
            Table = table;
            Generation = generation;
            Event = @event;
        }

        public Table Table { get; }
        public Generation Generation { get; }
        public EventData<byte[]> Event { get; }

        public static EventData<byte[]> Execute(DocumentTransaction tx, AppendEventCommand command)
        {
            var parameters = new DynamicParameters();

            if (command.Event.SequenceNumber == SequenceNumber.Any)
                throw new InvalidOperationException("SequenceNumber.Any is not currently supported.");

            if (command.Event.SequenceNumber < SequenceNumber.BeginningOfStream)
                throw new InvalidOperationException("SequenceNumber must be 0 or a positive integer.");

            if (string.IsNullOrWhiteSpace(command.Event.StreamId))
                throw new InvalidOperationException("StreamId must be set.");

            parameters.Add("@commitId", tx.CommitId);
            parameters.Add("@eventId", command.Event.EventId);
            parameters.Add("@stream", command.Event.StreamId);
            parameters.Add("@seq", command.Event.SequenceNumber);
            parameters.Add("@name", command.Event.Name);
            parameters.Add("@gen", command.Generation.ToString());
            parameters.Add("@data", command.Event.Data);
            parameters.Add("@meta", JsonConvert.SerializeObject(command.Event.Metadata.Values));

            var sql = $@"
                INSERT INTO {tx.Store.Database.FormatTableNameAndEscape(command.Table.Name)} (batch, id, stream, seq, name, gen, meta, data) 
                OUTPUT Inserted.globSeq, Inserted.seq
                VALUES (@commitId, @eventId, @stream, @seq, @name, @gen, @meta, @data)";

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