using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Threading;
using BoyBoy;
using FakeItEasy;
using HybridDb.Config;
using HybridDb.Queue;
using Newtonsoft.Json.Linq;
using ShouldBeLike;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using static HybridDb.Helpers;
using SqlException = Microsoft.Data.SqlClient.SqlException;
using Task = System.Threading.Tasks.Task;

namespace HybridDb.Tests.Queue
{
    public class HybridDbMessageQueueTests : HybridDbTests
    {
        readonly Func<IDocumentSession, HybridDbMessage, Task> handler;

        public HybridDbMessageQueueTests(ITestOutputHelper output) : base(output) =>
            handler = A.Fake<Func<IDocumentSession, HybridDbMessage, Task>>();

        HybridDbMessageQueue StartQueue(MessageQueueOptions options = null)
        {
            configuration.UseMessageQueue(
                (options ?? new MessageQueueOptions
                {
                    UseLocalEnqueueTrigger = false
                })
                .ReplayEvents(TimeSpan.FromSeconds(60)));

            return Using(new HybridDbMessageQueue(store,
                handler));
        }

        IDocumentStore CreateOtherStore(Action<Configuration> configurator)
        {
            var newConfiguration = new Configuration();

            newConfiguration.UseConnectionString(connectionString);

            configurator(newConfiguration);

            return Using(new DocumentStore(store,
                newConfiguration,
                true));
        }

        (HybridDbMessageQueue, IDocumentStore) StartOtherQueue(MessageQueueOptions options = null)
        {
            var newStore = CreateOtherStore(newConfiguration =>
            {
                newConfiguration.UseConnectionString(connectionString);
                newConfiguration.UseMessageQueue(
                    (options ?? new MessageQueueOptions
                    {
                        UseLocalEnqueueTrigger = false
                    })
                    .ReplayEvents(TimeSpan.FromSeconds(60)));
            });

            return (Using(new HybridDbMessageQueue(newStore,
                handler)), newStore);
        }

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

            message.Metadata.ShouldContainKeyAndValue("asger",
                "true");
        }

        [Fact]
        public async Task Enqueue_WithIdGenerator()
        {
            StartQueue();

            var subject = new ReplaySubject<HybridDbMessage>();

            handler.Call(asserter => subject.OnNext(asserter.Arguments.Get<HybridDbMessage>(1)));

            using var session = store.OpenSession();

            string IdGenerator(MyMessage m, Guid e) => $"{m.Text}/{e}";

            session.Enqueue(IdGenerator,
                new MyMessage("Some command"));

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
        public async Task DequeueAndHandle_ByInsertionOrder()
        {
            var queue = StartQueue();

            using (var session = store.OpenSession())
            {
                foreach (var i in Enumerable.Range(1,
                             100))
                {
                    session.Enqueue(new MyMessage(i.ToString()));
                }

                session.SaveChanges();
            }

            var messages = await queue.ReplayedEvents
                .OfType<MessageCommitted>()
                .Take(100)
                .ToList()
                .FirstAsync();

            messages
                .Select(x => x.Message.Payload)
                .Cast<MyMessage>()
                .Select(x => x.Text)
                .ShouldBe(Enumerable.Range(1,
                    100).Select(x => x.ToString()));
        }

        [Fact]
        public async Task DequeueAndHandle_ByOrder()
        {
            var queue = StartQueue();

            using var session = store.OpenSession();

            session.Enqueue(new MyMessage("order-default-1"));
            session.Enqueue(new MyMessage("order-default-2"));
            session.Enqueue(new MyMessage("order-2.1"),
                order: 2);

            session.Enqueue(new MyMessage("order-2.2"),
                order: 2);

            session.Enqueue(new MyMessage("order-1.1"),
                order: 1);

            session.Enqueue(new MyMessage("order-1.2"),
                order: 1);

            session.SaveChanges();

            var messages = await queue.ReplayedEvents
                .OfType<MessageCommitted>()
                .Take(6)
                .ToList()
                .FirstAsync();

            messages
                .Select(x => x.Message.Payload)
                .Cast<MyMessage>()
                .Select(x => x.Text)
                .ShouldBe(new List<string>
                {
                    "order-default-1",
                    "order-default-2",
                    "order-1.1",
                    "order-1.2",
                    "order-2.1",
                    "order-2.2"
                });
        }

        [Fact]
        public async Task Enqueue_Idempotent()
        {
            configuration.UseMessageQueue(new MessageQueueOptions()
                .ReplayEvents(TimeSpan.FromSeconds(60)));

            var id = Guid.NewGuid().ToString();

            using (var session = store.OpenSession())
            {
                session.Enqueue(id,
                    new MyMessage("A"));

                session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                session.Enqueue(id,
                    new MyMessage("B"));

                session.SaveChanges();
            }

            var queue = Using(new HybridDbMessageQueue(store,
                handler));

            var messages = new List<object>();
            queue.ReplayedEvents.OfType<MessageHandling>().Select(x => x.Message.Payload).Subscribe(messages.Add);
            await queue.ReplayedEvents.OfType<QueueEmpty>().FirstAsync();

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
                session.Enqueue(new MyMessage("Some command"));

                session.SaveChanges();
            }

            var messages = new List<object>();

            await queue.ReplayedEvents
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
                .Invokes(_ => throw new ArgumentException());

            using (var session = store.OpenSession())
            {
                session.Enqueue(new MyMessage("Failing command"));

                session.SaveChanges();
            }

            var messages = new List<object>();

            await queue.ReplayedEvents
                .Do(messages.Add)
                .OfType<PoisonMessage>()
                .FirstAsync();

            // PoisonMessage is found on the error topic
            var message = store.Execute(new DequeueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new List<string> { "errors/default" }));

            ((MyMessage)message.Payload).Text.ShouldBe("Failing command");

            // Original message is removed
            store.Execute(new DequeueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new List<string> { "default" })
            ).ShouldBe(null);
        }

        [Fact]
        public async Task Poison_GoesToErrorTopic_NothingElseIsSaved()
        {
            var queue = StartQueue();

            A.CallTo(handler).WithReturnType<Task>()
                .Invokes(x =>
                {
                    var session = x.Arguments.Get<IDocumentSession>(0);

                    session.Store("my-key",
                        new Entity());

                    throw new ArgumentException();
                });

            using (var session = store.OpenSession())
            {
                session.Enqueue(new MyMessage("Failing command"));

                session.SaveChanges();
            }

            var messages = new List<object>();

            await queue.ReplayedEvents
                .Do(messages.Add)
                .OfType<PoisonMessage>()
                .FirstAsync();

            using var session2 = store.OpenSession();

            // Nothing is saved
            session2.Load<Entity>("my-key").ShouldBe(null);

            // PoisonMessage is found on the error topic
            var message1 = store.Execute(new DequeueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new List<string> { "errors/default" }));

            ((MyMessage)message1.Payload).Text.ShouldBe("Failing command");

            // Original message is removed
            store.Execute(new DequeueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new List<string> { "default" })
            ).ShouldBe(null);
        }

        [Theory]
        [InlineData(null,
            typeof(SqlException))]
        [InlineData("5301a0b6-48bd-49ae-bd69-493e4fc802db",
            typeof(ConcurrencyException))]
        public async Task Poison_GoesToErrorTopic_NothingElseIsSaved_EvenForExceptionsDuringSaveChanges(string etag, Type exceptionType)
        {
            var queue = StartQueue();

            var i = 0;

            A.CallTo(handler).WithReturnType<Task>()
                .Invokes(x =>
                {
                    var session = x.Arguments.Get<IDocumentSession>(0);

                    i++;

                    if (i == 5)
                    {
                        // This should not be saved when my-key1 is not saved
                        session.Store("my-key2",
                            new Entity());
                    }

                    if (etag == null)
                    {
                        // This will fail with a primary key violation during SaveChanges
                        session.Store("my-key1",
                            new Entity());
                    }
                    else
                    {
                        // This will fail with ConcurrencyException during SaveChanges
                        session.Store("my-key1",
                            new Entity(),
                            new Guid(etag));
                    }
                });

            using (var session = store.OpenSession())
            {
                session.Store("my-key1",
                    new Entity());

                session.Enqueue(new MyMessage("Failing command"));

                session.SaveChanges();
            }

            var messages = new List<object>();

            var poisonMessage = await queue.ReplayedEvents
                .Do(messages.Add)
                .OfType<PoisonMessage>()
                .FirstAsync();

            poisonMessage.Exception.ShouldBeOfType(exceptionType);

            using var session2 = store.OpenSession();

            // Nothing is saved
            session2.Load<Entity>("my-key2").ShouldBe(null);

            // PoisonMessage is found on the error topic
            var message1 = store.Execute(new DequeueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new List<string> { "errors/default" }));

            ((MyMessage)message1.Payload).Text.ShouldBe("Failing command");

            // Original message is removed
            store.Execute(new DequeueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new List<string> { "default" })
            ).ShouldBe(null);
        }

        [Fact]
        public async Task Poison_GoesToErrorTopic_TopicIsReadFromColumn()
        {
            A.CallTo(handler).WithReturnType<Task>()
                .Invokes(_ => throw new ArgumentException());

            configuration.UseMessageQueue(new MessageQueueOptions
            {
                InboxTopics = ListOf("mytopic",
                    "myothertopic")
            }.ReplayEvents(TimeSpan.FromSeconds(60)));

            var id = Guid.NewGuid().ToString();

            using (var session = store.OpenSession())
            {
                session.Enqueue(id,
                    new MyMessage("Failing command"),
                    "mytopic");

                session.SaveChanges();
            }

            // Manipulate the topic directly in database - like returning errors to queue
            var queueTable = store.Configuration.Tables.Values.OfType<QueueTable>().Single();
            store.Database.RawExecute(
                $"update {store.Database.FormatTableNameAndEscape(queueTable.Name)} set [Topic] = 'myothertopic'");

            var messages = new List<object>();

            // start the queue late, after above manipulation
            var queue = Using(new HybridDbMessageQueue(store,
                handler));

            await queue.ReplayedEvents
                .Do(messages.Add)
                .OfType<PoisonMessage>()
                .FirstAsync();

            var message = store.Execute(new DequeueCommand(
                store.Configuration.Tables.Values.OfType<QueueTable>().Single(),
                new List<string> { "errors/myothertopic" }));

            message.Topic.ShouldBe("errors/myothertopic");
            ((MyMessage)message.Payload).Text.ShouldBe("Failing command");
        }

        [Fact]
        public async Task PoisonMessagesAreNotHandled()
        {
            var queue = StartQueue();

            using (var session = store.OpenSession())
            {
                session.Enqueue(new MyMessage("poison message"),
                    "errors");

                session.Enqueue(new MyMessage("edible message"));

                session.SaveChanges();
            }

            var messages = new List<MessageHandling>();

            await queue.ReplayedEvents
                .OfType<MessageHandling>()
                .Do(messages.Add)
                .TakeUntil(Observable.Timer(TimeSpan.FromSeconds(1)));

            ((MyMessage)messages.Single().Message.Payload).Text.ShouldBe("edible message");
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
            }.ReplayEvents(TimeSpan.FromSeconds(60))));

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

            var queue = Using(new HybridDbMessageQueue(store2,
                handler));

            var messages = await queue.ReplayedEvents
                .OfType<MessageCommitted>()
                .Take(5)
                .Select(x => x.Message)
                .ToList()
                .FirstAsync();

            messages.Select(x => x.Payload)
                .OfType<MyMessage>()
                .Select(x => x.Text)
                .ShouldBeLikeUnordered("1",
                    "2",
                    "3",
                    "4",
                    "the last one");
        }

        [Fact]
        public async Task DequeueAndHandle_NotLaterVersions()
        {
            configuration.UseMessageQueue(new MessageQueueOptions
            {
                Version = new Version("1.2.3")
            }.ReplayEvents(TimeSpan.FromSeconds(60)));

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

            var queue = Using(new HybridDbMessageQueue(store,
                handler));

            await Should.ThrowAsync<TimeoutException>(async () => await queue.ReplayedEvents
                .OfType<MessageCommitted>()
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

            var queue = Using(new HybridDbMessageQueue(store,
                (_, _) => Task.CompletedTask));

            var messages = await queue.ReplayedEvents
                .OfType<QueueEmpty>()
                .TakeUntil(Observable.Timer(TimeSpan.FromSeconds(7)))
                .ToList()
                .FirstAsync();

            // was around 70.000 rounds in 7secs on my pc
            // not that it really matters
            output.WriteLine($"{DateTime.Now.ToString("s").Replace("T", " ")} [Information] IdlePerformance: {messages.Count}");
        }

        [Fact]
        public async Task ReadPerformance()
        {
            configuration.UseMessageQueue(new MessageQueueOptions
            {
                MaxConcurrency = 100
            }.ReplayEvents(TimeSpan.FromSeconds(60)));

            using (var session = store.OpenSession())
            {
                foreach (var j in Enumerable.Range(1,
                             100000))
                {
                    session.Enqueue(new MyMessage(j.ToString()));
                }

                session.SaveChanges();
            }

            var queue = Using(new HybridDbMessageQueue(store,
                (_, _) => Task.CompletedTask));

            var messages = await queue.ReplayedEvents
                .OfType<MessageCommitted>()
                .TakeUntil(Observable.Timer(TimeSpan.FromSeconds(10)))
                .ToList()
                .FirstAsync();

            // around 6300 messages on my machine
            output.WriteLine($"{DateTime.Now.ToString("s").Replace("T", " ")} [Information] ReadPerformance: {messages.Count}");
        }

        [Fact]
        public void MultipleReader_OnlyOnePerStore()
        {
            configuration.UseMessageQueue(new MessageQueueOptions());

            Should.Throw<HybridDbException>(() => configuration
                    .UseMessageQueue(new MessageQueueOptions()))
                .Message.ShouldBe("Only one message queue can be enabled per store.");
        }

        [Fact(Skip = "flaky")]
        public async Task MultipleReaders()
        {
            var queue1 = StartQueue(new MessageQueueOptions { UseLocalEnqueueTrigger = false });
            var (queue2, _) = StartOtherQueue(new MessageQueueOptions { UseLocalEnqueueTrigger = false });
            var (queue3, _) = StartOtherQueue(new MessageQueueOptions { UseLocalEnqueueTrigger = false });

            using (var session = store.OpenSession())
            {
                foreach (var i in Enumerable.Range(1,
                             200))
                {
                    session.Enqueue(new MyMessage(i.ToString()));
                }

                session.SaveChanges();
            }

            var q1Count = 0;
            var q2Count = 0;
            var q3Count = 0;

            queue1.ReplayedEvents.OfType<MessageCommitted>().Subscribe(_ => q1Count++);
            queue2.ReplayedEvents.OfType<MessageCommitted>().Subscribe(_ => q2Count++);
            queue3.ReplayedEvents.OfType<MessageCommitted>().Subscribe(_ => q3Count++);

            var allDiagnostics = new List<IHybridDbQueueEvent>();

            var diagnostics = Observable
                .Merge(queue1.ReplayedEvents,
                    queue2.ReplayedEvents,
                    queue3.ReplayedEvents)
                .Do(allDiagnostics.Add);

            var handled = await diagnostics
                .OfType<MessageCommitted>()
                .Take(200)
                .ToList()
                .FirstAsync();

            // Each message is handled only once
            handled.Select(x => int.Parse(((MyMessage)x.Message.Payload).Text))
                .OrderBy(x => x).ShouldBe(Enumerable.Range(1,
                    200));

            // reasonably evenly load
            q1Count.ShouldBeGreaterThan(20);
            q2Count.ShouldBeGreaterThan(20);
            q3Count.ShouldBeGreaterThan(20);

            allDiagnostics.OfType<MessageFailed>().ShouldBeEmpty();
        }

        [Fact]
        public async Task MultipleWorkers_Default_4()
        {
            var max = new StrongBox<int>(0);

            configuration.UseMessageQueue(new MessageQueueOptions()
                .ReplayEvents(TimeSpan.FromSeconds(60)));

            var queue = Using(new HybridDbMessageQueue(store,
                MaxConcurrencyCounter(max)));

            using (var session = store.OpenSession())
            {
                foreach (var i in Enumerable.Range(1,
                             200))
                {
                    session.Enqueue(new MyMessage(i.ToString()));
                }

                session.SaveChanges();
            }

            await queue.ReplayedEvents.OfType<MessageCommitted>()
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
                foreach (var i in Enumerable.Range(1,
                             200))
                {
                    session.Enqueue(new MyMessage(i.ToString()));
                }

                session.SaveChanges();
            }

            var threads = await queue.ReplayedEvents.OfType<MessageCommitted>()
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
                session.Enqueue(new MyMessage("2"),
                    "a");

                session.Enqueue(new MyMessage("3"),
                    "b");

                session.Enqueue(new MyMessage("4"),
                    "a");

                session.SaveChanges();
            }

            var messages = await queue.ReplayedEvents
                .OfType<MessageCommitted>()
                .Take(2)
                .Select(x => x.Message)
                .ToList()
                .FirstAsync();

            messages.Select(x => x.Payload)
                .OfType<MyMessage>()
                .Select(x => x.Text)
                .ShouldBeLikeUnordered("2",
                    "4");
        }

        [Fact]
        public async Task Topics_MultipleTopics()
        {
            var queue = StartQueue(new MessageQueueOptions
            {
                InboxTopics = ListOf("a",
                    "b")
            });

            using (var session = store.OpenSession())
            {
                session.Enqueue(new MyMessage("1"));
                session.Enqueue(new MyMessage("2"),
                    "a");

                session.Enqueue(new MyMessage("3"),
                    "b");

                session.Enqueue(new MyMessage("4"),
                    "c");

                session.SaveChanges();
            }

            var messages = await queue.ReplayedEvents
                .OfType<MessageCommitted>()
                .Take(2)
                .Select(x => x.Message)
                .ToList()
                .FirstAsync();

            messages.Select(x => x.Payload)
                .OfType<MyMessage>()
                .Select(x => x.Text)
                .ShouldBeLikeUnordered("2",
                    "3");
        }

        [Fact]
        public async Task Topics_MultipleQueues()
        {
            var queue1 = StartQueue(new MessageQueueOptions
            {
                InboxTopics = ListOf("a",
                    "c")
            });

            var (queue2, _) = StartOtherQueue(new MessageQueueOptions
            {
                InboxTopics = ListOf("b",
                    "default")
            });

            using (var session = store.OpenSession())
            {
                session.Enqueue(new MyMessage("1"));
                session.Enqueue(new MyMessage("2"),
                    "a");

                session.Enqueue(new MyMessage("3"),
                    "b");

                session.Enqueue(new MyMessage("4"),
                    "a");

                session.Enqueue(new MyMessage("5"),
                    "c");

                session.SaveChanges();
            }

            var messagesA = await queue1.ReplayedEvents
                .OfType<MessageCommitted>()
                .Take(3)
                .Select(x => x.Message)
                .ToList()
                .FirstAsync();

            var messagesB = await queue2.ReplayedEvents
                .OfType<MessageCommitted>()
                .Take(2)
                .Select(x => x.Message)
                .ToList()
                .FirstAsync();

            messagesA.Select(x => x.Payload)
                .OfType<MyMessage>()
                .Select(x => x.Text)
                .ShouldBeLikeUnordered("2",
                    "4",
                    "5");

            messagesB.Select(x => x.Payload)
                .OfType<MyMessage>()
                .Select(x => x.Text)
                .ShouldBeLikeUnordered("1",
                    "3");
        }

        [Fact]
        public void CancellationDispose()
        {
            configuration.UseMessageQueue(new MessageQueueOptions
            {
                GetCancellationTokenSource = () => new CancellationTokenSource(0)
            });

            Should.NotThrow(() => new HybridDbMessageQueue(store,
                handler).Dispose());
        }

        [Fact]
        public async Task DequeueAndHandle_Timestamp()
        {
            var before = DateTimeOffset.Now;

            StartQueue();

            var subject = new ReplaySubject<HybridDbMessage>();

            handler.Call(asserter => subject.OnNext(asserter.Arguments.Get<HybridDbMessage>(1)));

            using (var session = store.OpenSession())
            {
                session.Enqueue(new MyMessage("Some command"));
                session.SaveChanges();
            }

            var message = await subject.FirstAsync();

            DateTimeOffset.TryParse(message.Metadata[HybridDbMessage.EnqueuedAtKey],
                out var date).ShouldBe(true);

            date.ShouldBeInRange(before,
                DateTimeOffset.Now);
        }

        [Fact]
        public async Task CorrelationIds()
        {
            StartQueue();

            var subject = new ReplaySubject<HybridDbMessage>();

            A.CallTo(handler).Invokes(call =>
            {
                call.Arguments.Get<IDocumentSession>(0)
                    .Enqueue("id2", new MyMessage("Next command"));

                subject.OnNext(call.Arguments.Get<HybridDbMessage>(1));
            });

            using (var session = store.OpenSession())
            {
                session.Enqueue("id1",
                    new MyMessage("Some command"));

                session.SaveChanges();
            }

            var messages = await subject.Take(2).ToList();

            messages[0].CorrelationId.ShouldBe("id1");
            messages[0].Metadata.ShouldContainKeyAndValue(HybridDbMessage.Breadcrumbs, new JArray("id1").ToString());

            messages[1].CorrelationId.ShouldBe("id1");
            messages[1].Metadata.ShouldContainKeyAndValue(HybridDbMessage.Breadcrumbs, new JArray("id1", "id2").ToString());
        }

        [Fact]
        public async Task CorrelationIds_Reset()
        {
            StartQueue();

            var subject = new ReplaySubject<HybridDbMessage>();

            A.CallTo(handler).Invokes(call =>
            {
                call.Arguments.Get<IDocumentSession>(0).Enqueue("id2", new MyMessage("Next command"), resetCorrelationIds: true);

                subject.OnNext(call.Arguments.Get<HybridDbMessage>(1));
            });

            using (var session = store.OpenSession())
            {
                session.Enqueue("id1", new MyMessage("Some command"));

                session.SaveChanges();
            }

            var messages = await subject.Take(2).ToList();

            messages[0].CorrelationId.ShouldBe("id1");
            messages[0].Metadata.ShouldContainKeyAndValue(HybridDbMessage.Breadcrumbs, new JArray("id1").ToString());

            messages[1].CorrelationId.ShouldBe("id2");
            messages[1].Metadata.ShouldContainKeyAndValue(HybridDbMessage.Breadcrumbs, new JArray("id1", "id2").ToString());
        }

        [Fact]
        public async Task CorrelationIds_WithIdGenerator()
        {
            StartQueue();

            var subject = new ReplaySubject<HybridDbMessage>();

            A.CallTo(handler).Invokes(call => subject.OnNext(call.Arguments.Get<HybridDbMessage>(1)));

            using (var session = store.OpenSession())
            {
                session.Enqueue((x, y) => "id1", new MyMessage("Some command"));

                session.SaveChanges();
            }

            var messages = await subject.Take(1).ToList();

            messages[0].CorrelationId.ShouldBe("id1");

            // TODO: Bug. See QueueEx.AddBreadcrumb
            //messages[0].Metadata.ShouldContainKeyAndValue(HybridDbMessage.Breadcrumbs, new JArray("id1").ToString());
        }

        [Fact]
        public async Task CorrelationIds_MoreMessages()
        {
            StartQueue();

            var subject = new ReplaySubject<HybridDbMessage>();

            A.CallTo(handler).Invokes(call =>
            {
                var incomingMessage = call.Arguments.Get<HybridDbMessage>(1);

                if (incomingMessage.Id == "id1")
                {
                    call.Arguments.Get<IDocumentSession>(0).Enqueue("id2",
                        new MyMessage("Next command"));

                    call.Arguments.Get<IDocumentSession>(0).Enqueue("id3",
                        new MyMessage("Next command"));
                }

                if (incomingMessage.Id == "id3")
                {
                    call.Arguments.Get<IDocumentSession>(0).Enqueue("id4",
                        new MyMessage("Next command"));
                }

                subject.OnNext(incomingMessage);
            });

            using (var session = store.OpenSession())
            {
                session.Enqueue("id1",
                    new MyMessage("Some command"));

                session.SaveChanges();
            }

            var messages = await subject.Take(4).ToList();
            var orderedMessages = messages.OrderBy(x => x.Id).ToList();

            orderedMessages[0].CorrelationId.ShouldBe("id1");
            orderedMessages[0].Metadata
                .ShouldContainKeyAndValue(HybridDbMessage.Breadcrumbs,
                    new JArray("id1").ToString());

            orderedMessages[1].CorrelationId.ShouldBe("id1");
            orderedMessages[1].Metadata
                .ShouldContainKeyAndValue(HybridDbMessage.Breadcrumbs,
                    new JArray("id1",
                        "id2").ToString());

            orderedMessages[2].CorrelationId.ShouldBe("id1");
            orderedMessages[2].Metadata
                .ShouldContainKeyAndValue(HybridDbMessage.Breadcrumbs,
                    new JArray("id1",
                        "id3").ToString());

            orderedMessages[3].CorrelationId.ShouldBe("id1");
            orderedMessages[3].Metadata.ShouldContainKeyAndValue(HybridDbMessage.Breadcrumbs,
                new JArray("id1",
                    "id3",
                    "id4").ToString());
        }

        [Fact]
        public async Task LocalTriggering()
        {
            var observer = new BlockingTestObserver(TimeSpan.FromSeconds(10));

            configuration.UseMessageQueue(
                new MessageQueueOptions
                {
                    IdleDelay = TimeSpan.FromMilliseconds(int.MaxValue), // never retry without trigger,
                    MaxConcurrency = 1,
                    Subscribe = observer.Subscribe
                });

            Using(new HybridDbMessageQueue(store,
                (_, message) => Task.CompletedTask));

            await observer.AdvanceUntil<QueueEmpty>();

            using (var session = store.OpenSession())
            {
                session.Enqueue("id1",
                    new MyMessage("Some command 1"));

                session.SaveChanges();
            }

            await observer.AdvanceBy1ThenNextShouldBe<QueuePolling>();
            await observer.AdvanceBy1ThenNextShouldBe<MessageReceived>();
        }

        [Fact]
        public async Task LocalTriggering_Many()
        {
            var observer = new BlockingTestObserver(TimeSpan.FromSeconds(10));

            configuration.UseMessageQueue(
                new MessageQueueOptions
                {
                    IdleDelay = TimeSpan.FromMilliseconds(int.MaxValue), // never retry without trigger,
                    MaxConcurrency = 1,
                    Subscribe = observer.Subscribe
                });

            Using(new HybridDbMessageQueue(store,
                (_, message) => Task.CompletedTask));

            await observer.AdvanceUntil<QueueEmpty>();

            using (var session = store.OpenSession())
            {
                session.Enqueue("id1",
                    new MyMessage("Some command 1"));

                session.Enqueue("id2",
                    new MyMessage("Some command 2"));

                session.Enqueue("id3",
                    new MyMessage("Some command 3"));

                session.SaveChanges();
            }

            await observer.AdvanceBy1();
            await observer.NextShouldBe<QueuePolling>();
            await observer.AdvanceUntil<MessageCommitted>();

            await observer.AdvanceBy1();
            await observer.NextShouldBe<QueuePolling>();
            await observer.AdvanceUntil<MessageCommitted>();

            await observer.AdvanceBy1();
            await observer.NextShouldBe<QueuePolling>();
            await observer.AdvanceUntil<MessageCommitted>();

            await observer.AdvanceBy1();
            await observer.NextShouldBe<QueuePolling>();
            await observer.AdvanceBy1();
            await observer.NextShouldBe<QueueEmpty>();
            await observer.AdvanceBy1();
            await observer.WaitForNothingToHappen();
        }

        [Fact]
        public async Task LocalTriggering_EnqueuedJustAfterQueueEmpty()
        {
            var observer = new BlockingTestObserver(TimeSpan.FromSeconds(10));

            configuration.UseMessageQueue(
                new MessageQueueOptions
                {
                    IdleDelay = TimeSpan.FromMilliseconds(int.MaxValue), // never retry without trigger,
                    MaxConcurrency = 1,
                    Subscribe = observer.Subscribe
                });

            Using(new HybridDbMessageQueue(store,
                (_, message) => Task.CompletedTask));

            using (var session = store.OpenSession())
            {
                session.Enqueue("id1",
                    new MyMessage("Some command 1"));

                session.SaveChanges();
            }

            await observer.NextShouldBeThenAdvanceBy1<QueueStarting>();
            await observer.NextShouldBeThenAdvanceBy1<QueuePolling>();
            await observer.NextShouldBeThenAdvanceBy1<MessageReceived>();
            await observer.NextShouldBeThenAdvanceBy1<MessageHandling>();
            await observer.NextShouldBeThenAdvanceBy1<MessageHandled>();
            await observer.NextShouldBeThenAdvanceBy1<MessageCommitted>();
            await observer.NextShouldBeThenAdvanceBy1<QueuePolling>();
            await observer.NextShouldBe<QueueEmpty>();

            using (var session = store.OpenSession())
            {
                session.Enqueue("id2",
                    new MyMessage("Some command 2"));

                session.SaveChanges();
            }

            await observer.AdvanceBy1();

            await observer.NextShouldBeThenAdvanceBy1<QueuePolling>();
            await observer.NextShouldBeThenAdvanceBy1<MessageReceived>();
            await observer.NextShouldBeThenAdvanceBy1<MessageHandling>();
            await observer.NextShouldBeThenAdvanceBy1<MessageHandled>();
            await observer.NextShouldBeThenAdvanceBy1<MessageCommitted>();
            await observer.NextShouldBeThenAdvanceBy1<QueuePolling>();
            await observer.NextShouldBeThenAdvanceBy1<QueueEmpty>();

            await observer.WaitForNothingToHappen();
        }

        [Fact]
        public async Task LocalTriggering_Topics()
        {
            var observer = new BlockingTestObserver(TimeSpan.FromSeconds(10));

            configuration.UseMessageQueue(
                new MessageQueueOptions
                {
                    IdleDelay = TimeSpan.FromMilliseconds(int.MaxValue), // never retry without trigger,
                    MaxConcurrency = 1,
                    InboxTopics = { "topic1" },
                    Subscribe = observer.Subscribe
                });

            Using(new HybridDbMessageQueue(store,
                (_, message) => Task.CompletedTask));

            await observer.AdvanceUntil<QueueEmpty>();

            using (var session = store.OpenSession())
            {
                session.Enqueue("id1",
                    new MyMessage("Some command 1"),
                    "topic1");

                session.SaveChanges();
            }

            await observer.AdvanceBy1();
            await observer.NextShouldBeThenAdvanceBy1<QueuePolling>();
            await observer.NextShouldBeThenAdvanceBy1<MessageReceived>();
            await observer.NextShouldBeThenAdvanceBy1<MessageHandling>();
            await observer.NextShouldBeThenAdvanceBy1<MessageHandled>();
            await observer.NextShouldBeThenAdvanceBy1<MessageCommitted>();
            await observer.NextShouldBeThenAdvanceBy1<QueuePolling>();
            await observer.NextShouldBe<QueueEmpty>();
        }

        [Fact]
        public async Task LocalTriggering_NotOtherTopics()
        {
            var observer = new BlockingTestObserver(TimeSpan.FromSeconds(10));

            configuration.UseMessageQueue(
                new MessageQueueOptions
                {
                    IdleDelay = TimeSpan.FromMilliseconds(int.MaxValue), // never retry without trigger,
                    MaxConcurrency = 1,
                    InboxTopics = { "topic1" },
                    Subscribe = observer.Subscribe
                }.ReplayEvents(TimeSpan.FromSeconds(60)));

            Using(new HybridDbMessageQueue(store,
                (_, message) => Task.CompletedTask));

            await observer.AdvanceUntil<QueueEmpty>();

            using (var session = store.OpenSession())
            {
                session.Enqueue("id1",
                    new MyMessage("Some command 1"),
                    "topics2");

                session.SaveChanges();
            }

            await observer.AdvanceBy1();
            await observer.WaitForNothingToHappen();
        }

        [Fact]
        public async Task FastAndFurious()
        {
            // This test is to asses parallel runs of many queues, as we often do this in application testing.
            // There's is no assertion, but it should run, handle and dispose all queues reasonably timely.

            void WriteLine(string s)
            {
                Debug.WriteLine(s);
                output.WriteLine(s);
            }

            async Task Selector(int x)
            {
                var tableName = $"Queue{x}";
                var subject = new ReplaySubject<int>();

                WriteLine($"[INFORMATION] #{x} Start");

                try
                {
                    var documentStore = DocumentStore.ForTesting(TableMode.GlobalTempTables,
                        cfg =>
                        {
                            cfg.DisableBackgroundMigrations();
                            cfg.UseConnectionString(connectionString);
                            cfg.UseMessageQueue(new MessageQueueOptions
                            {
                                IdleDelay = TimeSpan.Zero,
                                TableName = tableName,
                                MaxConcurrency = 1
                            }.ReplayEvents(TimeSpan.FromSeconds(60)));
                        });

                    WriteLine($"[INFORMATION] #{x} Started");

                    try
                    {
                        var eventLoopScheduler = new EventLoopScheduler();
                        using var queue = new HybridDbMessageQueue(documentStore,
                            async (_, _) =>
                            {
                                await Task.Delay(1000);
                                WriteLine($"[INFORMATION] #{x} message handled 1");
                                subject.OnNext(1);
                                WriteLine($"[INFORMATION] #{x} message handled 2");
                            });

                        using var session = documentStore.OpenSession();

                        session.Enqueue(new MyMessage("Some command"));
                        session.SaveChanges();

                        WriteLine($"[INFORMATION] #{x} message sent");

                        await Task.WhenAny(subject.ObserveOn(eventLoopScheduler).FirstAsync().ToTask(),
                            queue.MainLoop);

                        WriteLine($"[INFORMATION] #{x} Completed");
                    }
                    finally
                    {
                        documentStore.Dispose();

                        WriteLine($"[INFORMATION] #{x} Dispose");
                    }
                }
                catch (Exception ex)
                {
                    WriteLine($"[ERROR] #{x} {ex.Message}");
                }
            }

            await ParallelForEachAsync(Enumerable.Range(0,
                    100), Selector,
                200);

            WriteLine("All tasks completed");
        }

        Func<IDocumentSession, HybridDbMessage, Task> MaxConcurrencyCounter(StrongBox<int> max)
        {
            var counter = new SemaphoreSlim(int.MaxValue);

            return async (_, _) =>
            {
                await counter.WaitAsync();

                await Task.Delay(100);

                max.Value = Math.Max(int.MaxValue - counter.CurrentCount,
                    max.Value);

                counter.Release();
            };
        }

        static Task ParallelForEachAsync<T>(IEnumerable<T> source, Func<T, Task> funcBody, int maxDoP = 4)
        {
            async Task AwaitPartition(IEnumerator<T> partition)
            {
                using (partition)
                {
                    while (partition.MoveNext())
                    {
                        await Task.Yield(); // prevents a sync/hot thread hangup
                        await funcBody(partition.Current);
                    }
                }
            }

            return Task.WhenAll(
                Partitioner
                    .Create(source)
                    .GetPartitions(maxDoP)
                    .AsParallel()
                    .Select(AwaitPartition));
        }

        public record MyMessage(string Text);
    }
}