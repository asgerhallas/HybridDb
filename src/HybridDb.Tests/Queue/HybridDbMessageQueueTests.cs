using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using BoyBoy;
using FakeItEasy;
using HybridDb;
using HybridDb.Queue;
using HybridDb.Tests;
using Serilog;
using Serilog.Core;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Energy10.Tests.Application.Devices.MessageQueue
{
    public class HybridDbMessageQueueTests : HybridDbTests
    {
        readonly HybridDbMessageQueue queue;
        readonly Func<IDocumentSession, HybridDbMessage, Task> handler;

        public HybridDbMessageQueueTests(ITestOutputHelper output) : base(output)
        {
            configuration.UseMessageQueue();

            handler = A.Fake<Func<IDocumentSession, HybridDbMessage, Task>>();
            queue = Using(new HybridDbMessageQueue(store, handler, logger));
        }

        public record MyMessage(string Id, string Text) : HybridDbMessage(Id);

        [Fact]
        public async Task Dispatch()
        {
            var subject = new Subject<HybridDbMessage>();

            handler.Call(asserter =>
            {
                subject.OnNext(asserter.Arguments.Get<HybridDbMessage>(1));
            });

            using (var session = store.OpenSession())
            {
                session.Advanced.Defer(
                    new EnqueueCommand(configuration.Tables.Values.OfType<QueueTable>().Single(), 
                    new MyMessage(Guid.NewGuid().ToString(), "Some command")));

                session.SaveChanges();
            }

            var message = await subject.FirstAsync();

            message.ShouldBeOfType<MyMessage>()
                .Text.ShouldBe("Some command");
        }        
        
        [Fact]
        public async Task RetriesAFixedNumberOfTimes()
        {
            A.CallTo(handler).WithReturnType<Task>()
                .Invokes(x => throw new ArgumentException());
            
            using (var session = store.OpenSession())
            {
                session.Store(new MyMessage(Guid.NewGuid().ToString(), "Some command"));
                session.SaveChanges();
            }

            var messages = await queue.Handling.Select(x => x.Message).OfType<MyMessage>().Take(5).ToList().SingleAsync();

            messages.Select(x => x.Text).ShouldAllBe(x => x == "Some command");
        }
    }
}