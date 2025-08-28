using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using HybridDb.Events;
using HybridDb.Events.Commands;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests.Events
{
    public class ReadStreamTests : EventStoreTests
    {
        public ReadStreamTests(ITestOutputHelper output) : base(output)
        {
            UseEventStore();
        }

        [Fact]
        public void ReadsByStream()
        {
            store.Execute(
                CreateAppendEventCommand(CreateEventData("stream-1", 0)),
                CreateAppendEventCommand(CreateEventData("stream-1", 1)),
                CreateAppendEventCommand(CreateEventData("stream-2", 0)));

            var events = Execute(new ReadStream(new EventTable("events"), "stream-1", 0));

            events.Select(x => x.SequenceNumber).ShouldBe([0, 1]);
        }

        [Fact]
        public void LoadWithCutOffOnPosition()
        {
            store.Execute(CreateAppendEventCommand(CreateEventData("stream-1", 0)));
            store.Execute(CreateAppendEventCommand(CreateEventData("stream-1", 1)));
            store.Execute(CreateAppendEventCommand(CreateEventData("stream-1", 2)));

            var events = Execute(new ReadStream(new EventTable("events"), "stream-1", 0, 1));

            events.Select(x => x.SequenceNumber).ShouldBe([0, 1]);
        }

        [Fact]
        public void LoadWithCutOffOnPosition_OnDifferentStream()
        {
            store.Execute(CreateAppendEventCommand(CreateEventData("stream-0", 0)));
            store.Execute(CreateAppendEventCommand(CreateEventData("stream-1", 0)));
            store.Execute(CreateAppendEventCommand(CreateEventData("stream-1", 1)));
            store.Execute(CreateAppendEventCommand(CreateEventData("stream-1", 2)));

            var events = Execute(new ReadStream(new EventTable("events"), "stream-1", 0, 2));

            events.Select(x => x.SequenceNumber).ShouldBe([0, 1]);
        }

        [Fact]
        public void DoesNotStreamEntireCommitAnyMore()
        {
            // We've changed the way this works. Now the client of store.Load is responsible 
            // for supplying a position that is not mid-commit - if that is what is needed
            // This is for performance-reasons mostly - and because the client usually knows the correct position

            store.Execute(Enumerable.Range(0, 100).Select(i => CreateAppendEventCommand(CreateEventData("stream-0", i))));

            var count = Execute(new ReadStream(new EventTable("events"), "stream-0", 0, 9)).Count;

            count.ShouldBe(10);
        }

        [Fact]
        public void LastOrDefault()
        {
            store.Execute(Enumerable.Range(0, 100).Select(x => CreateAppendEventCommand(CreateEventData("stream-1", x))));

            var events = Execute(new ReadStream(new EventTable("events"), "stream-1", -1, direction: Direction.Backward));

            events.FirstOrDefault().SequenceNumber.ShouldBe(99);
        }

        [Fact]
        public void LastOrDefaultNoEvents()
        {
            store.Execute(Enumerable.Range(0, 100).Select(x => CreateAppendEventCommand(CreateEventData("stream-1", x))));

            var events = Execute(new ReadStream(new EventTable("events"), "some-other-id", -1, direction: Direction.Backward));

            events.ShouldBeEmpty();
        }

        [Fact]
        public void LoadEventsByStreamConcurrently()
        {
            store.Execute(Enumerable.Range(0, 1000).Select(x => CreateAppendEventCommand(CreateEventData("stream-1", x))));

            Parallel.For(0, 100, i =>
            {
                Execute(new ReadStream(new EventTable("events"), "stream-1", 0));
            });
        }

        [Fact]
        public void LoadEventsByStreamFromSeqNumber()
        {
            //bump up the global sequence number so it does not match the stream seq number
            store.Execute(Enumerable.Range(0, 10).Select(x => CreateAppendEventCommand(CreateEventData("stream-1", x))));
            store.Execute(Enumerable.Range(0, 10).Select(x => CreateAppendEventCommand(CreateEventData("stream-2", x))));

            var events = Execute(new ReadStream(new EventTable("events"), "stream-2", 5));

            events
                .Select(x => x.SequenceNumber)
                .ShouldBe([5, 6, 7, 8, 9]);
        }

        [Fact]
        public void LoadEventsByStreamWithCutoffAtPosition()
        {
            byte pos = 0;

            //bump up the global sequence number so it does not match the stream seq number
            store.Execute(Enumerable.Range(0, 10).Select(x => CreateAppendEventCommand(CreateEventData("stream-1", pos++))));
            store.Execute(Enumerable.Range(0, 6).Select(x => CreateAppendEventCommand(CreateEventData("stream-2", pos++))));
            store.Execute(Enumerable.Range(6, 4).Select(x => CreateAppendEventCommand(CreateEventData("stream-2", pos++))));

            var events = Execute(new ReadStream(new EventTable("events"), "stream-2", 0, 15));

            events
                .Select(x => x.SequenceNumber)
                .ShouldBe([10, 11, 12, 13, 14, 15]);
        }

        List<EventData<byte[]>> Execute(ReadStream command) => store.Transactionally(IsolationLevel.Snapshot, tx => tx
            .Execute(command)
            .SelectMany(x => x.Events)
            .ToList());
    }
}