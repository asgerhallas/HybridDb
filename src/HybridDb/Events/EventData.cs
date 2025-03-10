using System;

namespace HybridDb.Events
{
    public interface EventData
    {
        string StreamId { get; }
        Guid EventId { get; }
        string Name { get; }
        long SequenceNumber { get; }
        IReadOnlyMetadata Metadata { get; }

        EventData<TData> WithData<TData>(TData data);
    }

    public class EventData<T> : EventData
    {
        public EventData(string streamId, Guid eventId, string name, long sequenceNumber, IReadOnlyMetadata metadata, T data)
        {
            if (streamId == null) throw new ArgumentNullException(nameof(streamId));
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));
            if (data == null) throw new ArgumentNullException(nameof(data));

            StreamId = streamId;
            EventId = eventId;
            Name = name;
            SequenceNumber = sequenceNumber;
            Metadata = metadata;
            Data = data;
        }

        public string StreamId { get; }
        public Guid EventId { get; }
        public string Name { get; }
        public long SequenceNumber { get; }
        public IReadOnlyMetadata Metadata { get; }
        public T Data { get; }

        public EventData<T> WithSeq(long seq) => new(StreamId, EventId, Name, seq, Metadata, Data);
        public EventData<T> WithName(string name) => new(StreamId, EventId, name, SequenceNumber, Metadata, Data);
        public EventData<T> WithMetadataKey(string key, string value) => new(StreamId, EventId, Name, SequenceNumber, new Metadata(Metadata) { [key] = value }, Data);
        public EventData<T> WithoutMetadataKey(string key) => new(StreamId, EventId, Name, SequenceNumber, new Metadata(Metadata, key), Data);
        public EventData<T> WithStreamId(string streamId) => new(streamId, EventId, Name, SequenceNumber, Metadata, Data);
        public EventData<T> WithEventId(Guid eventId) => new(StreamId, eventId, Name, SequenceNumber, Metadata, Data);

        public EventData<TData> WithData<TData>(TData data) => new(StreamId, EventId, Name, SequenceNumber, Metadata, data);
    }
}