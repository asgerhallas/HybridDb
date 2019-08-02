using System;
using System.Collections.Generic;
using System.Linq;

namespace HybridDb.Events
{
    public abstract class Commit
    {
        protected Commit(Guid id, int generation, long begin, long end)
        {
            Id = id;
            Generation = generation;
            Begin = begin;
            End = end;
        }

        public Guid Id { get; }
        public int Generation { get; }
        public long Begin { get; }
        public long End { get; }

        public static Commit<T> Create<T>(Guid id, int generation, long end, IReadOnlyList<EventData<T>> events) => 
            new Commit<T>(id, generation, end + 1 - events.Count, end, events);

        public static Commit<T> Empty<T>() => new Commit<T>(Guid.Empty, 1, -1, -1);
    }

    public class Commit<T> : Commit
    {
        public Commit(Guid id, int generation, long begin, long end, params EventData<T>[] events)
            : this(id, generation, begin, end, events.ToList()) { }

        public Commit(Guid id, int generation, long begin, long end, IReadOnlyList<EventData<T>> events) 
            : base(id, generation, begin, end) => Events = events;

        public Commit<TOut> Map<TOut>(Func<EventData<T>, IEnumerable<EventData<TOut>>> selector) =>
            new Commit<TOut>(Id, Generation, Begin, End, Events.SelectMany(selector).ToList());

        public IReadOnlyList<EventData<T>> Events { get; }
    }
}