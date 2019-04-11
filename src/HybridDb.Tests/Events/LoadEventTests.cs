using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using HybridDb.Events;
using HybridDb.Events.Commands;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Events
{
    public class LoadEventTests : EventStoreTests
    {

        [Fact]
        public void LastOrDefault()
        {
            store.Execute(Enumerable.Range(0, 100).Select(x => CreateAppendEventCommand(CreateEventData("stream-1", x))));

            var events = store.Transactionally(tx => tx.Execute(new ReadStream(new EventTable("events"), "stream-1", -1, direction: Direction.Backward)).ToList());

            events.FirstOrDefault().SequenceNumber.ShouldBe(99);
        }

        [Fact]
        public void LastOrDefaultNoEvents()
        {
            store.Execute(Enumerable.Range(0, 100).Select(x => CreateAppendEventCommand(CreateEventData("stream-1", x))));

            var events = store.Transactionally(tx => tx.Execute(new ReadStream(new EventTable("events"), "some-other-id", -1, direction: Direction.Backward)).ToList());

            events.ShouldBeEmpty();
        }

        [Fact]
        public void LoadEventsByStreamConcurrently()
        {
            store.Execute(Enumerable.Range(0, 1000).Select(x => CreateAppendEventCommand(CreateEventData("stream-1", x))));

            Parallel.For(0, 100, i =>
            {
                store.Transactionally(tx => tx.Execute(new ReadStream(new EventTable("events"), "stream-1", 0)).ToList());
            });
        }

        [Fact]
        public void LoadEventsByStreamFromSeqNumber()
        {
            //bump up the global sequence number so it does not match the stream seq number
            store.Execute(Enumerable.Range(0, 10).Select(x => CreateAppendEventCommand(CreateEventData("stream-1", x))));
            store.Execute(Enumerable.Range(0, 10).Select(x => CreateAppendEventCommand(CreateEventData("stream-2", x))));

            var events = store.Transactionally(tx => tx.Execute(new ReadStream(new EventTable("events"), "stream-2", 5)).ToList());

            events
                .Select(x => x.SequenceNumber)
                .ShouldBe(new long[] { 5, 6, 7, 8, 9 });
        }

        [Fact]
        public void LoadEventsByStreamWithCutoffAtPosition()
        {
            byte pos = 0;

            //bump up the global sequence number so it does not match the stream seq number
            store.Execute(Enumerable.Range(0, 10).Select(x => CreateAppendEventCommand(CreateEventData("stream-1", pos++))));
            store.Execute(Enumerable.Range(0, 6).Select(x => CreateAppendEventCommand(CreateEventData("stream-2", pos++))));
            store.Execute(Enumerable.Range(6, 4).Select(x => CreateAppendEventCommand(CreateEventData("stream-2", pos++))));

            var events = store.Transactionally(tx => tx.Execute(new ReadStream(new EventTable("events"), "stream-2", 0, 15)).ToList());

            events
                .Select(x => x.SequenceNumber)
                .ShouldBe(new long[] { 10, 11, 12, 13, 14, 15 });
        }

        [Fact]
        public void LoadWhileSaving()
        {
            //ExecuteManyAppendEventCommands(store, "stream-1", 0, 2);

            //var enumerator = store.Load("stream-1", 0).GetEnumerator();
            //enumerator.MoveNext();

            //store.Save(EmitMany("stream-1", 2, 4));

            //enumerator.MoveNext().ShouldBe(true);
            //enumerator.MoveNext().ShouldBe(false);
            //enumerator.Dispose();
        }
    }
}