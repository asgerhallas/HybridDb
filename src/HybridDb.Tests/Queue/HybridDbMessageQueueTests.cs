using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using BoyBoy;
using FakeItEasy;
using HybridDb.Config;
using HybridDb.Queue;
using ShouldBeLike;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using static HybridDb.Helpers;

namespace HybridDb.Tests.Queue
{
    public class HybridDbMessageQueueTests : HybridDbTests
    {
        readonly Func<IDocumentSession, HybridDbMessage, Task> handler;

        public HybridDbMessageQueueTests(ITestOutputHelper output) : base(output)
        {
            handler = A.Fake<Func<IDocumentSession, HybridDbMessage, Task>>();
        }

        HybridDbMessageQueue StartQueue(MessageQueueOptions options = null)
        {
            configuration.UseMessageQueue(
                (options ?? new MessageQueueOptions())
                .ReplayEvents(TimeSpan.FromSeconds(60)));

            return Using(new HybridDbMessageQueue(store, handler));
        }

        IDocumentStore CreateOtherStore(Action<Configuration> configurator)
        {
            var newConfiguration = new Configuration();
            
            newConfiguration.UseConnectionString(connectionString);

            configurator(newConfiguration);
            return Using(new DocumentStore(store, newConfiguration, true));
        }

        (HybridDbMessageQueue, IDocumentStore) StartOtherQueue(MessageQueueOptions options = null)
        {
            var newStore = CreateOtherStore(newConfiguration =>
            {
                newConfiguration.UseConnectionString(connectionString);
                newConfiguration.UseMessageQueue(
                    (options ?? new MessageQueueOptions())
                    .ReplayEvents(TimeSpan.FromSeconds(60)));
            });

            return (Using(new HybridDbMessageQueue(newStore, handler)), newStore);
        }

        public record MyMessage(string Text);

        [Fact]
        public async Task DequeueAndHandle()
        {
            StartQueue();

            var subject = new ReplaySubject<HybridDbMessage>();

            handler.Call(asserter => subject.OnNext(asserter.Arguments.Get<HybridDbMessage>(1)));

            using (var session = store.OpenSession())
            {
                session.Enqueue(new MyMessage("Some command"));

                session.SaveChanges();
            }

            var message = await subject.FirstAsync();

            message.Payload.ShouldBeOfType<MyMessage>().Text.ShouldBe("Some command");
        }

        [Fact]
        public async Task ReadNumberOfMessages()
        {
            var queue = StartQueue();

            var subject = new ReplaySubject<HybridDbMessage>();

            //handler.Call(asserter => subject.OnNext(asserter.Arguments.Get<HybridDbMessage>(1)));
            handler.Call(asserter =>
            {
                using var tx = store.BeginTransaction(IsolationLevel.Snapshot);
                var result = tx.Execute(new ReadMessageStatsCommand(store.Configuration.Tables.Values.OfType<QueueTable>().Single()));

                subject.OnNext(asserter.Arguments.Get<HybridDbMessage>(1));
            });
            
            using (var session = store.OpenSession())
            {
                session.Enqueue(new MyMessage("Some command"));
                session.Enqueue(new MyMessage("Some command"));
                session.Enqueue(new MyMessage("Some command"));

                session.SaveChanges();
            }

            await queue.Events.OfType<MessageHandled>().FirstAsync();

            using var tx = store.BeginTransaction(IsolationLevel.Snapshot);
            var result = tx.Execute(new ReadMessageStatsCommand(store.Configuration.Tables.Values.OfType<QueueTable>().Single()));

            result.ShouldBe(2);
        }
        
        [Fact]
        public async Task DequeueAndHandle_Metadata()
        {
            StartQueue();

            var subject = new ReplaySubject<HybridDbMessage>();

            handler.Call(asserter => subject.OnNext(asserter.Arguments.Get<HybridDbMessage>(1)));

            using (var session = store.OpenSession())
            {
                session.Enqueue(
                    new MyMessage("Some command"),
                    metadata: new Dictionary<string, string>
                    {
                        ["asger"] = "true"
                    });

                session.SaveChanges();
            }

            var message = await subject.FirstAsync();

            message.Metadata.ShouldContainKeyAndValue("asger", "true");
        }
        
        [Fact]
        public async Task EnqueueWithIdGenerator()
        {
            StartQueue();

            var subject = new ReplaySubject<HybridDbMessage>();

            handler.Call(asserter => subject.OnNext(asserter.Arguments.Get<HybridDbMessage>(1)));

            using var session = store.OpenSession();

            string IdGenerator(MyMessage m, Guid e) => $"{m.Text}/{e}";

            session.Enqueue(IdGenerator, new MyMessage("Some command"));

            var etag = session.SaveChanges();

            var message = await subject.FirstAsync();

            message.Id.ShouldBe($"Some command/{etag}");
        }

        [Fact]
        public async Task DequeueAndHandle_AsyncHandler_ThatContinuesOnOtherThread()
        {
            StartQueue();

            var subject = new ReplaySubject<HybridDbMessage>();

            A.CallTo(handler).WithReturnType<Task>()
                .Invokes(async x =>
                {
                    output.WriteLine(Thread.CurrentThread.ManagedThreadId.ToString());

                    await Task.Yield();
                    await Task.Delay(1000);

                    output.WriteLine(Thread.CurrentThread.ManagedThreadId.ToString());

                    subject.OnNext(x.Arguments.Get<HybridDbMessage>(1));
                });

            using (var session = store.OpenSession())
            {
                session.Enqueue(new MyMessage("Some command"));

                session.SaveChanges();
            }

            var message = await subject.FirstAsync();

            message.Payload.ShouldBeOfType<MyMessage>().Text.ShouldBe("Some command");
        }

        [Fact]
        public async Task DequeueAndHandle_AfterMuchIdle()
        {
            StartQueue(new MessageQueueOptions
            {
                IdleDelay = TimeSpan.Zero
            });

            var subject = new ReplaySubject<HybridDbMessage>();

            handler.Call(asserter => subject.OnNext(asserter.Arguments.Get<HybridDbMessage>(1)));

            await Task.Delay(5000);

            using (var session = store.OpenSession())
            {
                session.Enqueue(new MyMessage("Some command"));

                session.SaveChanges();
            }

            var message = await subject.FirstAsync();

            message.Payload.ShouldBeOfType<MyMessage>().Text.ShouldBe("Some command");
        }

        [Fact]
        public async Task Enqueue_Idempotent()
        {
            configuration.UseMessageQueue(new MessageQueueOptions()
                .ReplayEvents(TimeSpan.FromSeconds(60)));

            var id = Guid.NewGuid().ToString();

            using (var session = store.OpenSession())
            {
                session.Enqueue(id, new MyMessage("A"));

                session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                session.Enqueue(id, new MyMessage("B"));

                session.SaveChanges();
            }

            var queue = Using(new HybridDbMessageQueue(store, handler));

            var messages = new List<object>();
            queue.Events.OfType<MessageHandling>().Select(x => x.Message.Payload).Subscribe(messages.Add);
            await queue.Events.OfType<QueueIdle>().FirstAsync();

            messages.Count.ShouldBe(1);
            ((MyMessage) messages.Single()).Text.ShouldBe("A");
        }

        [Fact]
        public async Task Retry_ThenPoison()
        {
            var queue = StartQueue();

            A.CallTo(handler).WithReturnType<Task>()
                .Invokes(x => throw new ArgumentException());

            using (var session = store.OpenSession())
            {
                session.Enqueue(new MyMessage("Some command"));

                session.SaveChanges();
            }

            var messages = new List<object>();

            await queue.Events
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
        public async Task Poison_GoesToErrorTopic()
        {
            var queue = StartQueue();

            A.CallTo(handler).WithReturnType<Task>()
                .Invokes(x => throw new ArgumentException());

            using (var session = store.OpenSession())
            {
                session.Enqueue(new MyMessage("Failing command"));

                session.SaveChanges();
            }

            var messages = new List<object>();

            await queue.Events
                .Do(messages.Add)
                .OfType<PoisonMessage>()
                .FirstAsync();

            var message = store.Execute(new DequeueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new List<string> {"errors/default"}));

            ((MyMessage)message.Payload).Text.ShouldBe("Failing command");
        }

        [Fact]
        public async Task Poison_GoesToErrorTopic_TopicIsReadFromColumn()
        {
            A.CallTo(handler).WithReturnType<Task>()
                .Invokes(x => throw new ArgumentException());

            configuration.UseMessageQueue(new MessageQueueOptions
            {
                InboxTopics = ListOf("mytopic", "myothertopic")
            }.ReplayEvents(TimeSpan.FromSeconds(60)));

            var id = Guid.NewGuid().ToString();

            using (var session = store.OpenSession())
            {
                session.Enqueue(id, new MyMessage("Failing command"), "mytopic");
                session.SaveChanges();
            }

            // Manipulate the topic directly in database - like returning errors to queue
            var queueTable = store.Configuration.Tables.Values.OfType<QueueTable>().Single();
            store.Database.RawExecute(
                $"update {store.Database.FormatTableNameAndEscape(queueTable.Name)} set [Topic] = 'myothertopic'");

            var messages = new List<object>();

            // start the queue late, after above manipulation
            var queue = Using(new HybridDbMessageQueue(store, handler));

            await queue.Events
                .Do(messages.Add)
                .OfType<PoisonMessage>()
                .FirstAsync();

            var message = store.Execute(new DequeueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new List<string> {"errors/myothertopic"}));

            message.Topic.ShouldBe("errors/myothertopic");
            ((MyMessage)message.Payload).Text.ShouldBe("Failing command");
        }

        [Fact]
        public async Task PoisonMessagesAreNotHandled()
        {
            var queue = StartQueue();

            using (var session = store.OpenSession())
            {
                session.Enqueue(new MyMessage("poison message"), "errors");

                session.Enqueue(new MyMessage("edible message"));

                session.SaveChanges();
            }

            var messages = new List<MessageHandling>();

            await queue.Events
                .OfType<MessageHandling>()
                .Do(messages.Add)
                .TakeUntil(Observable.Timer(TimeSpan.FromSeconds(1)));

            ((MyMessage) messages.Single().Message.Payload).Text.ShouldBe("edible message");
        }

        [Fact]
        public async Task DequeueAndHandle_SameOrEarlierVersion()
        {
            configuration.UseMessageQueue(new MessageQueueOptions
            {
                Version = new Version("1.2.3")
            });

            var store2 = CreateOtherStore(c => c.UseMessageQueue(new MessageQueueOptions
            {
                Version = new Version("1.2.4")
            }));
            
            var store3 = CreateOtherStore(c => c.UseMessageQueue(new MessageQueueOptions
            {
                Version = new Version("1.2.5")
            }));

            using (var session = store.OpenSession())
            {
                session.Enqueue(new MyMessage("1"));
                session.Enqueue(new MyMessage("2"));

                session.SaveChanges();
            }

            using (var session = store2.OpenSession())
            {
                session.Enqueue(new MyMessage("3"));
                session.Enqueue(new MyMessage("4"));

                session.SaveChanges();
            }

            using (var session = store3.OpenSession())
            {
                session.Enqueue(new MyMessage("5"));
                session.Enqueue(new MyMessage("6"));

                session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                session.Enqueue(new MyMessage("the last one"));

                session.SaveChanges();
            }

            var queue = Using(new HybridDbMessageQueue(store2, handler));

            var messages = await queue.Events
                .OfType<MessageHandled>()
                .Take(5)
                .Select(x => x.Message)
                .ToList()
                .FirstAsync();

            messages.Select(x => x.Payload)
                .OfType<MyMessage>()
                .Select(x => x.Text)
                .ShouldBeLikeUnordered("1", "2", "3", "4", "the last one");
        }        
        
        [Fact]
        public async Task DequeueAndHandle_NotLaterVersions()
        {
            configuration.UseMessageQueue(new MessageQueueOptions
            {
                Version = new Version("1.2.3")
            });

            var store2 = CreateOtherStore(c => c.UseMessageQueue(new MessageQueueOptions
            {
                Version = new Version("1.2.4")
            }));

            using (var session = store2.OpenSession())
            {
                session.Enqueue(new MyMessage("later1"));
                session.Enqueue(new MyMessage("later2"));

                session.SaveChanges();
            }

            var queue = Using(new HybridDbMessageQueue(store, handler));

            await Should.ThrowAsync<TimeoutException>(async () => await queue.Events
                .OfType<MessageHandled>()
                .FirstAsync()
                .Timeout(TimeSpan.FromSeconds(15)));
        }

        [Fact]
        public async Task IdlePerformance()
        {
            configuration.UseMessageQueue(
                new MessageQueueOptions
                {
                    IdleDelay = TimeSpan.Zero
                }.ReplayEvents(TimeSpan.FromSeconds(60)));

            var queue = Using(new HybridDbMessageQueue(store, (_, _) => Task.CompletedTask));

            var messages = await queue.Events
                .OfType<QueueIdle>()
                .TakeUntil(Observable.Timer(TimeSpan.FromSeconds(7)))
                .ToList()
                .FirstAsync();

            // was around 70.000 rounds in 7secs on my pc
            // not that it really matters
            output.WriteLine(messages.Count.ToString());
        }

        [Fact]
        public async Task ReadPerformance()
        {
            configuration.UseMessageQueue(new MessageQueueOptions
            {
                MaxConcurrency = 10,
            }.ReplayEvents(TimeSpan.FromSeconds(60)));

            using (var session = store.OpenSession())
            {
                foreach (var i in Enumerable.Range(1, 10000))
                    session.Enqueue(new MyMessage(i.ToString()));

                session.SaveChanges();
            }

            var queue = Using(new HybridDbMessageQueue(store, (_, _) => Task.CompletedTask));

            var messages = await queue.Events
                .OfType<MessageHandled>()
                .TakeUntil(Observable.Timer(TimeSpan.FromSeconds(10)))
                .ToList()
                .FirstAsync();

            // around 6300 messages on my machine
            output.WriteLine(messages.Count.ToString());
        }

        [Fact]
        public void MultipleReader_OnlyOnePerStore()
        {
            configuration.UseMessageQueue(new MessageQueueOptions());

            Should.Throw<HybridDbException>(() => configuration
                .UseMessageQueue(new MessageQueueOptions()))
                .Message.ShouldBe("Only one message queue can be enabled per store.");
        }

        [Fact]
        public async Task MultipleReaders()
        {
            var queue1 = StartQueue();
            var (queue2, _) = StartOtherQueue();
            var (queue3, _) = StartOtherQueue();

            using (var session = store.OpenSession())
            {
                foreach (var i in Enumerable.Range(1, 200))
                {
                    session.Enqueue(new MyMessage(i.ToString()));
                }

                session.SaveChanges();
            }

            var q1Count = 0;
            var q2Count = 0;
            var q3Count = 0;

            queue1.Events.OfType<MessageHandled>().Subscribe(_ => q1Count++);
            queue2.Events.OfType<MessageHandled>().Subscribe(_ => q2Count++);
            queue3.Events.OfType<MessageHandled>().Subscribe(_ => q3Count++);

            var allDiagnostics = new List<IHybridDbQueueEvent>();

            var diagnostics = Observable
                .Merge(queue1.Events, queue2.Events, queue3.Events)
                .Do(allDiagnostics.Add);

            var handled = await diagnostics
                .OfType<MessageHandled>()
                .Take(200)
                .ToList()
                .FirstAsync();

            // Each message is handled only once
            handled.Select(x => int.Parse(((MyMessage) x.Message.Payload).Text))
                .OrderBy(x => x).ShouldBe(Enumerable.Range(1, 200));

            // reasonably evenly load
            q1Count.ShouldBeGreaterThan(30);
            q2Count.ShouldBeGreaterThan(30);
            q3Count.ShouldBeGreaterThan(30);

            allDiagnostics.OfType<MessageFailed>().ShouldBeEmpty();
        }

        [Fact]
        public async Task MultipleWorkers_Default_4()
        {
            var max = new StrongBox<int>(0);

            configuration.UseMessageQueue(new MessageQueueOptions()
                .ReplayEvents(TimeSpan.FromSeconds(60)));

            var queue = Using(new HybridDbMessageQueue(store, MaxConcurrencyCounter(max)));

            using (var session = store.OpenSession())
            {
                foreach (var i in Enumerable.Range(1, 200))
                {
                    session.Enqueue(new MyMessage(i.ToString()));
                }

                session.SaveChanges();
            }

            await queue.Events.OfType<MessageHandled>()
                .Take(200)
                .ToList()
                .FirstAsync();

            max.Value.ShouldBe(4);
        }

        [Fact]
        public async Task MultipleWorkers_Limited_1()
        {
            var max = new StrongBox<int>(0);

            configuration.UseMessageQueue(new MessageQueueOptions
            {
                MaxConcurrency = 1
            }.ReplayEvents(TimeSpan.FromSeconds(60)));

            var queue = Using(new HybridDbMessageQueue(
                store,
                MaxConcurrencyCounter(max)));

            using (var session = store.OpenSession())
            {
                foreach (var i in Enumerable.Range(1, 200))
                {
                    session.Enqueue(new MyMessage(i.ToString()));
                }

                session.SaveChanges();
            }

            var threads = await queue.Events.OfType<MessageHandled>()
                .Take(200)
                .ToList()
                .FirstAsync();

            max.Value.ShouldBe(1);
        }

        [Fact]
        public async Task Topics()
        {
            var queue = StartQueue(new MessageQueueOptions
            {
                InboxTopics = ListOf("a")
            });

            using (var session = store.OpenSession())
            {
                session.Enqueue(new MyMessage("1"));
                session.Enqueue(new MyMessage("2"), "a");
                session.Enqueue(new MyMessage("3"), "b");
                session.Enqueue(new MyMessage("4"), "a");

                session.SaveChanges();
            }

            var messages = await queue.Events
                .OfType<MessageHandled>()
                .Take(2)
                .Select(x => x.Message)
                .ToList()
                .FirstAsync();

            messages.Select(x => x.Payload)
                .OfType<MyMessage>()
                .Select(x => x.Text)
                .ShouldBeLikeUnordered("2", "4");
        }

        [Fact]
        public async Task Topics_MultipleTopics()
        {
            var queue = StartQueue(new MessageQueueOptions
            {
                InboxTopics = ListOf("a", "b")
            });

            using (var session = store.OpenSession())
            {
                session.Enqueue(new MyMessage("1"));
                session.Enqueue(new MyMessage("2"), "a");
                session.Enqueue(new MyMessage("3"), "b");
                session.Enqueue(new MyMessage("4"), "c");

                session.SaveChanges();
            }

            var messages = await queue.Events
                .OfType<MessageHandled>()
                .Take(2)
                .Select(x => x.Message)
                .ToList()
                .FirstAsync();

            messages.Select(x => x.Payload)
                .OfType<MyMessage>()
                .Select(x => x.Text)
                .ShouldBeLikeUnordered("2", "3");
        }

        [Fact]
        public async Task Topics_MultipleQueues()
        {
            var queue1 = StartQueue(new MessageQueueOptions
            {
                InboxTopics = ListOf("a", "c")
            });

            var (queue2, _) = StartOtherQueue(new MessageQueueOptions
            {
                InboxTopics = ListOf("b", "default")
            });

            using (var session = store.OpenSession())
            {
                session.Enqueue(new MyMessage("1"));
                session.Enqueue(new MyMessage("2"), "a");
                session.Enqueue(new MyMessage("3"), "b");
                session.Enqueue(new MyMessage("4"), "a");
                session.Enqueue(new MyMessage("5"), "c");

                session.SaveChanges();
            }

            var messagesA = await queue1.Events
                .OfType<MessageHandled>()
                .Take(3)
                .Select(x => x.Message)
                .ToList()
                .FirstAsync();

            var messagesB = await queue2.Events
                .OfType<MessageHandled>()
                .Take(2)
                .Select(x => x.Message)
                .ToList()
                .FirstAsync();

            messagesA.Select(x => x.Payload)
                .OfType<MyMessage>()
                .Select(x => x.Text)
                .ShouldBeLikeUnordered("2", "4", "5");

            messagesB.Select(x => x.Payload)
                .OfType<MyMessage>()
                .Select(x => x.Text)
                .ShouldBeLikeUnordered("1", "3");
        }

        [Fact]
        public void CancellationDispose()
        {
            configuration.UseMessageQueue(new MessageQueueOptions
            {
                GetCancellationTokenSource = () => new CancellationTokenSource(0)
            });

            Should.NotThrow(() => new HybridDbMessageQueue(store, handler).Dispose());
        }

        Func<IDocumentSession, HybridDbMessage, Task> MaxConcurrencyCounter(StrongBox<int> max)
        {
            var counter = new SemaphoreSlim(int.MaxValue);

            return async (IDocumentSession _, HybridDbMessage _) =>
            {
                await counter.WaitAsync();

                await Task.Delay(100);

                max.Value = Math.Max(int.MaxValue - counter.CurrentCount, max.Value);

                counter.Release();
            };
        }
    }
}