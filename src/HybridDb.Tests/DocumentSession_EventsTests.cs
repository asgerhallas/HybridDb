using System;
using System.Linq;
using HybridDb.Commands;
using HybridDb.Queue;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests
{
    public class DocumentSession_EventsTests : HybridDbTests
    {
        public DocumentSession_EventsTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Events_SaveChanges_BeforeExecuteCommands()
        {
            Document<Case>();
            Document<Profile>();

            configuration.AddEventHandler(@event =>
            {
                if (@event is not SaveChanges_BeforeExecuteCommands savingChanges) return;

                foreach (var (managedEntity, dmlCommand) in savingChanges.DocumentCommands)
                {
                    if (managedEntity.Design.DocumentType != typeof(Case)) continue;
                    if (dmlCommand is not UpdateCommand && dmlCommand is not DeleteCommand) continue;

                    var profile = savingChanges.Session.Load<Profile>(((Case)managedEntity.Entity).ProfileId);

                    if (!profile.CanWrite) throw new Exception($"Can not execute {dmlCommand.GetType().Name}!");

                    ((Case) managedEntity.Entity).Text = "hullabulla"; 
                }
            });

            using (var session = store.OpenSession())
            {
                session.Store(new Profile("asger", true));
                session.Store(new Profile("danny", false));

                session.Store(new Case("case1", "asger", "a"));
                session.Store(new Case("case2", "danny", "a"));

                session.SaveChanges();
                session.Advanced.Clear();

                var c1 = session.Load<Case>("case1");
                c1.Text = "b";

                session.SaveChanges();

                var c2 = session.Load<Case>("case2");
                c2.Text = "b";

                Should.Throw<Exception>(() => session.SaveChanges())
                    .Message.ShouldBe("Can not execute UpdateCommand!");
            }

            using (var session = store.OpenSession())
            {
                var c1 = session.Load<Case>("case1");
                session.Delete(c1);
                session.SaveChanges();

                var c2 = session.Load<Case>("case2");
                session.Delete(c2);

                Should.Throw<Exception>(() => session.SaveChanges())
                    .Message.ShouldBe("Can not execute DeleteCommand!");
            }
        }

        [Fact]
        public void Events_SaveChanges_AfterExecuteCommands()
        {
            Document<Case>();
            Document<Profile>();

            configuration.UseMessageQueue();

            SaveChanges_AfterExecuteCommands result = null;
            configuration.AddEventHandler(@event =>
            {
                if (@event is not SaveChanges_AfterExecuteCommands savingChanges) return;

                result = savingChanges;
            });

            using var session = store.OpenSession();

            session.Store(new Profile("asger", true));
            session.Enqueue((m, e) => "a", new object());

            var commitId = session.SaveChanges();

            result.CommitId.ShouldBe(commitId);
            var executedCommands = result.ExecutedCommands.ToList();

            executedCommands[0].Key.ShouldBeOfType<InsertCommand>();
            executedCommands[0].Value.ShouldBe(commitId);

            executedCommands[1].Key.ShouldBeOfType<EnqueueCommand>();
            executedCommands[1].Value.ShouldBe("a");
        }

        public record Case(string Id, string ProfileId, string Text)
        {
            public string Text { get; set; } = Text;
        }

        public record Profile(string Id, bool CanWrite);
    }
}