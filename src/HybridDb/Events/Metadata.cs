using System;
using System.Collections.Generic;
using System.Linq;

namespace HybridDb.Events
{
    public class Metadata : IReadOnlyMetadata
    {
        static readonly HashSet<string> builtInKeys = new HashSet<string>();

        readonly Dictionary<string, string> values = new Dictionary<string, string>();

        /// <summary>
        /// Unique identifier for the event.
        /// </summary>
        public static readonly string EventId = Key("event_id");

        /// <summary>
        /// Sequence number local to an aggregate root instance.
        /// </summary>
        public static readonly string SequenceNumber = Key("seq");

        /// <summary>
        /// The ID of the aggregate root instance that emitted the event.
        /// </summary>
        public static readonly string StreamId = Key("stream_id");

        /// <summary>
        /// Date/time/offset of when the event was originally emitted
        /// </summary>
        public static readonly string Time = Key("date_time_offset");

        public static readonly string SchemaVersion = Key("schema_version");

        public static readonly string ContentType = Key("content_type");

        public static readonly string CorrelationId = Key("correlation_id");

        public static readonly string CommandId = Key("command_id");

        public Metadata() { }

        public Metadata(IReadOnlyMetadata data, params string[] omit) : this(data.Values, omit) {}

        public Metadata(IReadOnlyDictionary<string, string> data, params string[] omit)
        {
            TryAdd(data, omit);
        }

        public static string[] BuiltInKeys => builtInKeys.ToArray();

        public IReadOnlyDictionary<string, string> Values => values;

        public bool ContainsKey(string key) => values.ContainsKey(key);

        public string this[string key]
        {
            get
            {
                string value;
                if (!values.TryGetValue(key, out value))
                    throw new InvalidOperationException($"Key '{key}' was not found in metadata.");

                return value;
            }
            set { values[key] = value; }
        }

        public bool TryGetValue(string key, out string value) => values.TryGetValue(key, out value);

        public string TryGetValue(string key)
        {
            string value;
            return values.TryGetValue(key, out value) ? value : null;
        } 

        public void TryAdd(string key, string value)
        {
            if (!values.ContainsKey(key))
                values.Add(key, value);
        }

        public void TryAdd(IReadOnlyMetadata data, params string[] omit) => TryAdd(data.Values, omit);

        public void TryAdd(IReadOnlyDictionary<string, string> data, params string[] omit)
        {
            foreach (var keyValuePair in data.Where(x => !omit.Contains(x.Key)))
            {
                TryAdd(keyValuePair.Key, keyValuePair.Value);
            }
        }

        public void Add(string key, string value) => values.Add(key, value);

        public void Remove(string key) => values.Remove(key);

        static string Key(string key)
        {
            builtInKeys.Add(key);
            return key;
        }
    }
}