using System;
using System.Linq;
using HybridDb.Commands;
using HybridDb.Events;
using HybridDb.Queue;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests.Documentation;

public class Doc05_EventsTests(ITestOutputHelper output) : DocumentationTestBase(output)
{
    [Fact]
    public void AddEventHandler()
    {
        #region AddEventHandler
        var store = DocumentStore.Create(config =>
        {
            config.AddEventHandler(@event =>
            {
                if (@event is SaveChanges_BeforeExecuteCommands beforeSave)
                {
                    // Handle before save event
                    Console.WriteLine($"About to execute {beforeSave.DocumentCommands.Count()} commands");
                }
                
                if (@event is SaveChanges_AfterExecuteCommands afterSave)
                {
                    // Handle after save event
                    Console.WriteLine($"Executed {afterSave.ExecutedCommands.Count()} commands");
                }
            });
        });
        #endregion

        store.Dispose();
    }

    [Fact]
    public void BeforeExecuteCommands_Validation()
    {
        UseRealTables();
        Document<Case>();
        Document<Profile>();

        #region BeforeExecuteCommands_Validation
        configuration.AddEventHandler(@event =>
        {
            if (@event is not SaveChanges_BeforeExecuteCommands savingChanges) return;

            foreach (var (managedEntity, command) in savingChanges.DocumentCommands)
            {
                if (managedEntity.Design.DocumentType != typeof(Case)) continue;
                if (command is not UpdateCommand && command is not DeleteCommand) continue;

                var caseDoc = (Case)managedEntity.Entity;
                var profile = savingChanges.Session.Load<Profile>(caseDoc.ProfileId);

                if (!profile.CanWrite)
                {
                    throw new Exception($"User {profile.Id} cannot execute {command.GetType().Name}");
                }
            }
        });
        #endregion

        using (var session = store.OpenSession())
        {
            session.Store(new Profile("user1", true));
            session.Store(new Profile("user2", false));
            session.Store(new Case("case1", "user1", "Initial"));
            session.Store(new Case("case2", "user2", "Initial"));
            session.SaveChanges();
        }

        using (var session = store.OpenSession())
        {
            var case1 = session.Load<Case>("case1");
            case1.Text = "Updated";
            session.SaveChanges(); // Should succeed

            session.Advanced.Clear();

            var case2 = session.Load<Case>("case2");
            case2.Text = "Updated";
            
            Should.Throw<Exception>(() => session.SaveChanges())
                .Message.ShouldContain("cannot execute UpdateCommand");
        }
    }

    [Fact]
    public void BeforeExecuteCommands_Modification()
    {
        UseRealTables();
        Document<Case>();

        #region BeforeExecuteCommands_Modification
        configuration.AddEventHandler(@event =>
        {
            if (@event is not SaveChanges_BeforeExecuteCommands savingChanges) return;

            foreach (var (managedEntity, _) in savingChanges.DocumentCommands)
            {
                if (managedEntity.Entity is Case caseDoc)
                {
                    // Automatically add timestamp
                    caseDoc.Text = $"[{DateTime.UtcNow:yyyy-MM-dd}] {caseDoc.Text}";
                }
            }
        });
        #endregion

        using (var session = store.OpenSession())
        {
            session.Store(new Case("case1", "user1", "Test"));
            session.SaveChanges();
        }

        using (var session = store.OpenSession())
        {
            var case1 = session.Load<Case>("case1");
            case1.Text.ShouldStartWith("[");
        }
    }

    [Fact]
    public void AfterExecuteCommands_Logging()
    {
        UseRealTables();
        Document<Profile>();
        configuration.UseMessageQueue();

        #region AfterExecuteCommands_Logging
        configuration.AddEventHandler(@event =>
        {
            if (@event is not SaveChanges_AfterExecuteCommands afterSave) return;

            foreach (var (command, result) in afterSave.ExecutedCommands)
            {
                if (command is InsertCommand insert)
                {
                    Console.WriteLine($"Inserted document with commit ID: {result}");
                }
                else if (command is UpdateCommand update)
                {
                    Console.WriteLine($"Updated document with commit ID: {result}");
                }
            }
            
            Console.WriteLine($"Total commit ID: {afterSave.CommitId}");
        });
        #endregion

        using (var session = store.OpenSession())
        {
            session.Store(new Profile("user1", true));
            var commitId = session.SaveChanges();
            commitId.ShouldNotBe(Guid.Empty);
        }
    }

    [Fact]
    public void MultipleEventHandlers()
    {
        #region MultipleEventHandlers
        var store = DocumentStore.Create(config =>
        {
            // First handler - validation
            config.AddEventHandler(@event =>
            {
                if (@event is SaveChanges_BeforeExecuteCommands beforeSave)
                {
                    // Validate documents
                }
            });

            // Second handler - auditing
            config.AddEventHandler(@event =>
            {
                if (@event is SaveChanges_AfterExecuteCommands afterSave)
                {
                    // Log to audit trail
                }
            });

            // Third handler - notifications
            config.AddEventHandler(@event =>
            {
                if (@event is SaveChanges_AfterExecuteCommands afterSave)
                {
                    // Send notifications
                }
            });
        });
        #endregion

        store.Dispose();
    }

    #region SupportingTypes
    public record Case(string Id, string ProfileId, string Text)
    {
        public string Text { get; set; } = Text;
    }

    public record Profile(string Id, bool CanWrite);
    #endregion
}
