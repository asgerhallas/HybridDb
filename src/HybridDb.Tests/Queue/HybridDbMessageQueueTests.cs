using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using BoyBoy;
using FakeItEasy;
using HybridDb.Linq.Bonsai;
using HybridDb.Queue;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests.Queue
{
    public class HybridDbMessageQueueTests : HybridDbTests
    {
        readonly Func<IDocumentSession, HybridDbMessage, Task> handler;

        public HybridDbMessageQueueTests(ITestOutputHelper output) : base(output)
        {
            configuration.UseMessageQueue();

            handler = A.Fake<Func<IDocumentSession, HybridDbMessage, Task>>();
        }

        HybridDbMessageQueue StartQueue() => Using(new HybridDbMessageQueue(store, handler));

        public record MyMessage(string Id, string Text) : HybridDbMessage(Id);

        [Fact]
        public async Task DequeueAndHandle()
        {
            StartQueue();

            var subject = new Subject<HybridDbMessage>();

            handler.Call(asserter => subject.OnNext(asserter.Arguments.Get<HybridDbMessage>(1)));

            using (var session = store.OpenSession())
            {
                session.Enqueue(new MyMessage(Guid.NewGuid().ToString(), "Some command"));

                session.SaveChanges();
            }

            var message = await subject.FirstAsync();

            message.ShouldBeOfType<MyMessage>().Text.ShouldBe("Some command");
        }       
        
        [Fact]
        public async Task Enqueue_Idempotent()
        {
            var id = Guid.NewGuid().ToString();

            using (var session = store.OpenSession())
            {
                session.Enqueue(new MyMessage(id, "A"));

                session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                session.Enqueue(new MyMessage(id, "B"));

                session.SaveChanges();
            }

            var queue = StartQueue();

            var messages = new List<object>();
            queue.Diagnostics.OfType<MessageHandling>().Select(x => x.Message).Subscribe(messages.Add);
            await queue.Diagnostics.OfType<QueueIdle>().FirstAsync();

            messages.Count.ShouldBe(1);
            ((MyMessage)messages.Single()).Text.ShouldBe("A");
        }        
        
        [Fact]
        public async Task Retry_ThenPoison()
        {
            var queue = StartQueue();

            A.CallTo(handler).WithReturnType<Task>()
                .Invokes(x => throw new ArgumentException());
            
            using (var session = store.OpenSession())
            {
                session.Enqueue(new MyMessage(Guid.NewGuid().ToString(), "Some command"));

                session.SaveChanges();
            }

            var messages = new List<object>();

            await queue.Diagnostics
                .Do(messages.Add)
                .OfType<PoisonMessage>()
                .FirstAsync();

            var messageHandlings = messages.OfType<MessageHandling>().ToList();
            messageHandlings.Count.ShouldBe(5);
            messageHandlings
                .Select(x => x.Message).OfType<MyMessage>()
                .Select(x => x.Text).ShouldAllBe(x => x == "Some command");

            messages.OfType<MessageFailed>()
                .Select(x => x.Message).ShouldBe(messageHandlings
                    .Select(x => x.Message));
        }

        [Fact]
        public async Task PoisonMessagesAreNotHandled()
        {
            var queue = StartQueue();

            using (var session = store.OpenSession())
            {
                session.Enqueue(new MyMessage(Guid.NewGuid().ToString(), "poison message"), "errors");

                session.Enqueue(new MyMessage(Guid.NewGuid().ToString(), "edible message"));

                session.SaveChanges();
            }

            var messages = new List<MessageHandling>();

            await queue.Diagnostics
                .OfType<MessageHandling>()
                .Do(messages.Add)
                .TakeUntil(Observable.Timer(TimeSpan.FromSeconds(1)));

            ((MyMessage) messages.Single().Message).Text.ShouldBe("edible message");
        }

        [Fact]
        public async Task DontHawkConnectionsWhileIdle()
        {
            var diagnostics = Enumerable.Range(0, 200)
                .Select(x => Using(
                    new HybridDbMessageQueue(store, handler,
                        new MessageQueueOptions
                        {
                            IdleDelay = TimeSpan.FromSeconds(5)
                        })).Diagnostics)
                .Merge();

            var messages = await diagnostics
                .OfType<QueueFailed>()
                .TakeUntil(Observable.Timer(TimeSpan.FromSeconds(7)))
                .ToList()
                .FirstAsync();

            messages.ShouldBeEmpty();
        }

        [Fact]
        public async Task MultipleReaders()
        {
            using (var session = store.OpenSession())
            {
                foreach (var i in Enumerable.Range(1, 200))
                {
                    session.Enqueue(new MyMessage(Guid.NewGuid().ToString(), i.ToString()));
                }

                session.SaveChanges();
            }

            var queue1 = StartQueue();
            var queue2 = StartQueue();
            var queue3 = StartQueue();

            var q1Count = 0;
            var q2Count = 0;
            var q3Count = 0;
            
            queue1.Diagnostics.OfType<MessageHandled>().Subscribe(_ => q1Count++);
            queue2.Diagnostics.OfType<MessageHandled>().Subscribe(_ => q2Count++);
            queue3.Diagnostics.OfType<MessageHandled>().Subscribe(_ => q3Count++);

            var allDiagnostics = new List<IHybridDbDiagnosticEvent>();

            var diagnostics = Observable
                .Merge(queue1.Diagnostics, queue2.Diagnostics, queue3.Diagnostics)
                .Do(allDiagnostics.Add);

            var handled = await diagnostics
                .OfType<MessageHandled>()
                .Take(200)
                .ToList()
                .FirstAsync();

            // Each message is handled only once
            handled.Select(x => int.Parse(((MyMessage)x.Message).Text))
                .OrderBy(x => x).ShouldBe(Enumerable.Range(1, 200));

            // reasonably evenly load
            q1Count.ShouldBeGreaterThan(50);
            q2Count.ShouldBeGreaterThan(50);
            q3Count.ShouldBeGreaterThan(50);

            allDiagnostics.OfType<MessageFailed>().ShouldBeEmpty();
        }
    }
}