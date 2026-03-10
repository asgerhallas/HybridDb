# Configuration - Events

## Event Handlers Overview

HybridDb allows you to hook into the document lifecycle by registering event handlers. These handlers are called during session operations, allowing you to:

- Validate documents before they're saved
- Modify documents automatically (e.g., add timestamps, audit fields)
- Log operations for auditing
- Enforce business rules
- Send notifications after changes
- Implement cross-document validations

## Available Events

### SaveChanges_BeforePrepareCommands

Fires at the very beginning of `SaveChanges()`, before commands are prepared.

**Properties:**
- `Session`: The current document session

**Use cases:**
- Early validation
- Pre-processing before command preparation

### SaveChanges_BeforeExecuteCommands

Fires before any commands are executed during `SaveChanges()`. This is the right place to:
- Validate entities
- Modify entities before saving
- Perform cross-document checks
- Enforce business rules

**Properties:**
- `Session`: The current document session
- `DocumentCommands`: Dictionary of `(ManagedEntity, Command)` pairs about to be executed
- `OtherCommands`: List of other commands to be executed

### SaveChanges_AfterExecuteCommands

Fires after all commands have been successfully executed. This is the right place to:
- Log completed operations
- Send notifications
- Trigger side effects
- Record audit trails

**Properties:**
- `Session`: The current document session
- `ExecutedCommands`: Dictionary of `(Command, Result)` pairs that were executed
- `CommitId`: The unique identifier for this save operation

### EntityLoaded

Fires when an entity is loaded from the database.

**Properties:**
- `Session`: The current document session
- `RequestedType`: The type that was requested to be loaded
- `ManagedEntity`: The loaded entity

**Use cases:**
- Post-load processing
- Lazy loading related data
- Tracking entity access

### AddedToSession

Fires when an entity is added to the session (via `Store()` or `Load()`).

**Properties:**
- `Session`: The current document session
- `ManagedEntity`: The entity that was added

**Use cases:**
- Tracking new entities
- Setting default values
- Initializing relationships

### RemovedFromSession

Fires when an entity is removed from the session (via `Delete()` or `Evict()`).

**Properties:**
- `Session`: The current document session
- `ManagedEntity`: The entity that was removed

**Use cases:**
- Cleanup operations
- Cascade delete handling
- Tracking deletions

### MigrationStarted

Fires when a migration begins.

**Properties:**
- `Store`: The document store

**Use cases:**
- Pre-migration validation
- Logging migration start
- Taking backups

### MigrationEnded

Fires when a migration completes.

**Properties:**
- `Store`: The document store

**Use cases:**
- Post-migration validation
- Logging migration completion
- Cleanup operations

## Registering Event Handlers

### Basic Event Handler

Register an event handler during store configuration:

<!-- snippet: AddEventHandler -->
<a id='snippet-AddEventHandler'></a>

```cs
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
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc05_EventsTests.cs#L17-L35' title='Snippet source file'>snippet source</a> | <a href='#snippet-AddEventHandler' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Multiple Event Handlers

You can register multiple event handlers - they will be called in the order they were registered:

<!-- snippet: MultipleEventHandlers -->
<a id='snippet-MultipleEventHandlers'></a>

```cs
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
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc05_EventsTests.cs#L111-L141' title='Snippet source file'>snippet source</a> | <a href='#snippet-MultipleEventHandlers' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Common Use Cases

### Validation Before Save

Validate documents or enforce business rules before they're saved:

<!-- snippet: BeforeExecuteCommands_Validation -->
<a id='snippet-BeforeExecuteCommands_Validation'></a>

```cs
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
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc05_EventsTests.cs#L41-L60' title='Snippet source file'>snippet source</a> | <a href='#snippet-BeforeExecuteCommands_Validation' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

This example:
1. Checks every document being updated or deleted
2. Loads related data (Profile) to check permissions
3. Throws an exception if the operation is not allowed
4. The exception prevents the entire `SaveChanges()` from committing

### Automatic Document Modification

Modify documents automatically before they're saved:

<!-- snippet: BeforeExecuteCommands_Modification -->
<a id='snippet-BeforeExecuteCommands_Modification'></a>

```cs
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
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc05_EventsTests.cs#L66-L80' title='Snippet source file'>snippet source</a> | <a href='#snippet-BeforeExecuteCommands_Modification' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Common automatic modifications:
- Adding timestamps (`CreatedAt`, `ModifiedAt`)
- Setting audit fields (`CreatedBy`, `ModifiedBy`)
- Calculating derived values
- Normalizing data
- Adding default values

### Logging and Auditing

Log operations after they're successfully saved:

<!-- snippet: AfterExecuteCommands_Logging -->
<a id='snippet-AfterExecuteCommands_Logging'></a>

```cs
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
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc05_EventsTests.cs#L86-L105' title='Snippet source file'>snippet source</a> | <a href='#snippet-AfterExecuteCommands_Logging' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Advanced Scenarios

### Don't Modify Session State in AfterExecuteCommands

The `AfterExecuteCommands` event fires after the transaction is committed. Don't try to modify documents:

<!-- snippet: DontModifySessionStateInAfterExecuteCommands -->
<a id='snippet-DontModifySessionStateInAfterExecuteCommands'></a>

```cs
// Wrong - transaction already committed
if (@event is SaveChanges_AfterExecuteCommands afterSave)
{
    var doc = afterSave.Session.Load<Document>("id");
    doc.Field = "changed"; // This will not be saved

    // And calling afterSave.Session.SaveChanges() will cause an infinite loop
}
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc05_EventsTests.cs#L149-L158' title='Snippet source file'>snippet source</a> | <a href='#snippet-DontModifySessionStateInAfterExecuteCommands' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

If you need to change the documents before save, use SaveChanges_BEforeExecuteCommands. And if you need to spawn new work in a new session, Enqueue a message instead.

### Integration Events

Publish integration events before save:

<!-- snippet: IntegrationEvents -->
<a id='snippet-IntegrationEvents'></a>

```cs
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
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc05_EventsTests.cs#L165-L184' title='Snippet source file'>snippet source</a> | <a href='#snippet-IntegrationEvents' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Troubleshooting

### Infinite Loops

Avoid triggering saves within event handlers:

<!-- snippet: InfiniteLoops -->
<a id='snippet-InfiniteLoops'></a>

```cs
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
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc05_EventsTests.cs#L190-L201' title='Snippet source file'>snippet source</a> | <a href='#snippet-InfiniteLoops' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Performance Issues

If SaveChanges is slow, check your event handlers:
- Avoid loading too many documents
- Don't perform expensive operations in BeforeExecuteCommands
- Consider batching or queueing work for background processing
- Use AfterExecuteCommands for non-critical operations

### Transaction Scope

Remember the transaction boundaries:
- `BeforeExecuteCommands`: Transaction not yet started - safe to throw exceptions
- `AfterExecuteCommands`: Transaction committed - exceptions won't rollback the save
