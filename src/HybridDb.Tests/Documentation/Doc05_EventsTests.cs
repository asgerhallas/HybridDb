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
    [Fact(Skip = "Code example - not meant for execution")]
    public void AddEventHandler()
    {
        #region AddEventHandler
        using var store = DocumentStore.Create(config =>
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
    }

    [Fact(Skip = "Code example - not meant for execution")]
    public void BeforeExecuteCommands_Validation()
    {
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
    }

    [Fact(Skip = "Code example - not meant for execution")]
    public void BeforeExecuteCommands_Modification()
    {
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
    }

    [Fact(Skip = "Code example - not meant for execution")]
    public void AfterExecuteCommands_Logging()
    {
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
    }

    [Fact(Skip = "Code example - not meant for execution")]
    public void MultipleEventHandlers()
    {
        #region MultipleEventHandlers
        using var store = DocumentStore.Create(config =>
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
    }

    [Fact(Skip = "Code example - not meant for execution")]
    public void DontModifySessionStateInAfterExecuteCommands()
    {
        configuration.AddEventHandler(@event =>
        {
            #region DontModifySessionStateInAfterExecuteCommands
            // Wrong - transaction already committed
            if (@event is SaveChanges_AfterExecuteCommands afterSave)
            {
                var doc = afterSave.Session.Load<Document>("id");
                doc.Field = "changed"; // This will not be saved

                // And calling afterSave.Session.SaveChanges() will cause an infinite loop
            }
            #endregion
        });
    }

    [Fact(Skip = "Code example - not meant for execution")]
    public void IntegrationEvents()
    {
        #region IntegrationEvents
        configuration.AddEventHandler(@event =>
        {
            if (@event is not SaveChanges_BeforeExecuteCommands beforeSave) return;

            foreach (var (managedEntity, command) in beforeSave.DocumentCommands)
            {
                if (command is InsertCommand)
                {
                    if (managedEntity.Entity is Order order)
                    {
                        beforeSave.Session.Enqueue(new OrderCreatedEvent
                        {
                            OrderId = order.Id
                        });
                    }
                }
            }
        });
        #endregion
    }

    [Fact(Skip = "Code example - not meant for execution")]
    public void InfiniteLoops()
    {
        #region InfiniteLoops
        // Wrong - infinite loop!
        configuration.AddEventHandler(@event =>
        {
            if (@event is SaveChanges_BeforeExecuteCommands beforeSave)
            {
                var doc = new Document();
                beforeSave.Session.Store(doc);
                beforeSave.Session.SaveChanges(); // Triggers the event again!
            }
        });
        #endregion
    }

    #region SupportingTypes
    public record Case(string Id, string ProfileId, string Text)
    {
        public string Text { get; set; } = Text;
    }

    public record Profile(string Id, bool CanWrite);
    
    
    public record OrderCreatedEvent
    {
        public string OrderId { get; set; }
    }
    
    public class Document
    {
        public string Id { get; set; }
        public string Field { get; set; }
    }
    #endregion
}
