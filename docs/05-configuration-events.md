# Configuration - Events

## Event Store Overview

HybridDb includes an optional event store feature that allows you to store and retrieve events in a durable, ordered manner. This is useful for:

- Event sourcing architectures
- Audit trails
- Event-driven systems
- Cross-aggregate coordination
- CQRS implementations

## Enabling the Event Store

Enable the event store in your configuration:

```csharp
var store = DocumentStore.Create(config =>
{
    config.UseConnectionString(connectionString);
    config.UseEventStore();
});
```

This creates an event table in your database with the following structure:
- **Id**: Unique commit ID
- **Generation**: Version number for the commit
- **StreamId**: Identifier for the event stream
- **SequenceNumber**: Position within the stream
- **EventId**: Unique identifier for the event
- **Name**: Event type name
- **Data**: Serialized event data (binary)
- **Metadata**: Event metadata (JSON)
- **Position**: Global position in the event store

## Event Data

Events are represented by the `EventData<T>` class:

```csharp
public class EventData<T>
{
    public string StreamId { get; }
    public Guid EventId { get; }
    public string Name { get; }
    public long SequenceNumber { get; }
    public IReadOnlyMetadata Metadata { get; }
    public T Data { get; }
}
```

## Appending Events

### Basic Event Append

Append events to a stream using the session:

```csharp
using var session = store.OpenSession();

    var eventData = new EventData<byte[]>(
        streamId: "order-123",
        eventId: Guid.NewGuid(),
        name: "OrderCreated",
        sequenceNumber: 0,
        metadata: new Metadata(),
        data: Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { OrderId = "123" }))
    );
    
    session.Append(generation: 0, eventData);
    session.SaveChanges();
}
```

### Appending Multiple Events

Append multiple events in the same commit:

```csharp
using var session = store.OpenSession();

    var event1 = new EventData<byte[]>(
        streamId: "order-123",
        eventId: Guid.NewGuid(),
        name: "OrderCreated",
        sequenceNumber: 0,
        metadata: new Metadata(),
        data: SerializeEvent(new OrderCreatedEvent())
    );
    
    var event2 = new EventData<byte[]>(
        streamId: "order-123",
        eventId: Guid.NewGuid(),
        name: "ItemAdded",
        sequenceNumber: 1,
        metadata: new Metadata(),
        data: SerializeEvent(new ItemAddedEvent())
    );
    
    session.Append(0, event1);
    session.Append(0, event2);
    
    session.SaveChanges();
}
```

### Automatic Sequence Numbers

Use `SequenceNumber.Any` to let HybridDb assign sequence numbers automatically:

```csharp
using var session = store.OpenSession();

    var event1 = new EventData<byte[]>(
        streamId: "order-123",
        eventId: Guid.NewGuid(),
        name: "OrderCreated",
        sequenceNumber: SequenceNumber.Any,  // Automatically assigned
        metadata: new Metadata(),
        data: eventData
    );
    
    session.Append(0, event1);
    session.SaveChanges();
}
```

HybridDb will automatically assign the next available sequence number in the stream.

## Reading Events

### Read from Stream

Read all events from a specific stream:

```csharp
var commits = store.Transactionally(tx =>
{
    var table = store.Configuration.Tables.Values.OfType<EventTable>().Single();
    return tx.Execute(new ReadStream(table, "order-123", fromSequenceNumber: 0))
        .ToList();
});

foreach (var commit in commits)
{
    foreach (var eventData in commit.Events)
    {
        Console.WriteLine($"Event: {eventData.Name}, Seq: {eventData.SequenceNumber}");
```

### Read All Events

Read all events from a specific position:

```csharp
var commits = store.Transactionally(IsolationLevel.Snapshot, tx =>
{
    var table = store.Configuration.Tables.Values.OfType<EventTable>().Single();
    return tx.Execute(new ReadEvents(table, fromPosition: 0, readPastActiveTransactions: false))
        .ToList();
});

foreach (var commit in commits)
{
    Console.WriteLine($"Commit {commit.Id} with {commit.Events.Count} events");
    
    foreach (var eventData in commit.Events)
    {
        Console.WriteLine($"  {eventData.Name} at position {eventData.SequenceNumber}");
```

### Read by Commit IDs

Read specific commits by their IDs:

```csharp
var commitIds = new[] { commitId1, commitId2, commitId3 };

var commits = store.Transactionally(tx =>
{
    var table = store.Configuration.Tables.Values.OfType<EventTable>().Single();
    return tx.Execute(new ReadEventsByCommitIds(table, commitIds))
        .ToList();
});
```

## Event Metadata

Events can include metadata for additional context:

```csharp
var metadata = new Metadata
{
    ["UserId"] = "user-123",
    ["CorrelationId"] = correlationId.ToString(),
    ["CausationId"] = causationId.ToString(),
    ["Timestamp"] = DateTime.UtcNow.ToString("o")
};

var eventData = new EventData<byte[]>(
    streamId: "order-123",
    eventId: Guid.NewGuid(),
    name: "OrderCreated",
    sequenceNumber: 0,
    metadata: metadata,
    data: serializedData
);
```

### Reading Metadata

```csharp
foreach (var commit in commits)
{
    foreach (var eventData in commit.Events)
    {
        if (eventData.Metadata.TryGetValue("UserId", out var userId))
        {
            Console.WriteLine($"Event created by user: {userId}");
        }
```

## Commits and Generations

### Commit Structure

A commit represents a group of events saved together:

```csharp
public class Commit<T>
{
    public Guid Id { get; }
    public int Generation { get; }
    public long Begin { get; }     // First event position
    public long End { get; }       // Last event position
    public IReadOnlyList<EventData<T>> Events { get; }
}
```

### Generations

Generations allow for versioning of the event store schema:

```csharp
// Generation 0: Initial version
session.Append(generation: 0, eventData);

// Generation 1: After a migration
session.Append(generation: 1, eventData);
```

This is useful when you need to migrate event schemas.

## Concurrency Control

### Stream Concurrency

Prevent concurrent writes to the same stream:

```csharp
try
{
     using var session = store.OpenSession();

    var event = new EventData<byte[]>(
        streamId: "order-123",
        eventId: Guid.NewGuid(),
        name: "OrderUpdated",
        sequenceNumber: 5,  // Expecting this to be the next sequence
        metadata: new Metadata(),
        data: eventData
    );
    
    session.Append(0, event);
    session.SaveChanges();
catch (ConcurrencyException)
{
    // Another process already appended event with sequence number 5
    // Handle the conflict
}
```

### Event ID Uniqueness

Each event must have a unique EventId across all streams:

```csharp
var eventId = Guid.NewGuid();

// First append succeeds
session.Append(0, new EventData<byte[]>("stream-1", eventId, "Event", 0, new Metadata(), data));
session.SaveChanges();

// Second append with same EventId fails
session.Append(0, new EventData<byte[]>("stream-2", eventId, "Event", 0, new Metadata(), data));
session.SaveChanges(); // Throws ConcurrencyException
```

## Event Projections

Build read models from events:

```csharp
public class OrderProjection
{
    private readonly IDocumentStore store;
    private long lastProcessedPosition = 0;
    
    public async Task ProjectEvents()
    {
    var commits = store.Transactionally(IsolationLevel.Snapshot, tx =>
    {
        var table = store.Configuration.Tables.Values.OfType<EventTable>().Single();
        return tx.Execute(new ReadEvents(table, lastProcessedPosition, false))
            .ToList();
    });
    
    foreach (var commit in commits)
    {
        foreach (var eventData in commit.Events)
        {
            await HandleEvent(eventData);
        }
        
        lastProcessedPosition = commit.End + 1;
    }
    }
    
    private async Task HandleEvent(EventData<byte[]> eventData)
    {
    switch (eventData.Name)
    {
        case "OrderCreated":
            // Create order document
            break;
        case "ItemAdded":
            // Update order document
            break;
        case "OrderShipped":
            // Update order status
            break;
    }
```

## Event Store Patterns

### Event Sourced Aggregate

```csharp
public class Order
{
    private readonly List<EventData<byte[]>> uncommittedEvents = new();
    
    public string Id { get; private set; }
    public string Status { get; private set; }
    public List<OrderItem> Items { get; } = new();
    
    // Apply events to rebuild state
    public void Apply(EventData<byte[]> eventData)
    {
    switch (eventData.Name)
    {
        case "OrderCreated":
            var created = Deserialize<OrderCreatedEvent>(eventData.Data);
            Id = created.OrderId;
            Status = "Created";
            break;
            
        case "ItemAdded":
            var itemAdded = Deserialize<ItemAddedEvent>(eventData.Data);
            Items.Add(new OrderItem { ProductId = itemAdded.ProductId, Quantity = itemAdded.Quantity });
            break;
    }
    }
    
    // Command: Create order
    public static Order Create(string orderId)
    {
    var order = new Order();
    order.RaiseEvent("OrderCreated", new OrderCreatedEvent { OrderId = orderId });
    return order;
    }
    
    // Command: Add item
    public void AddItem(string productId, int quantity)
    {
    RaiseEvent("ItemAdded", new ItemAddedEvent { ProductId = productId, Quantity = quantity });
    }
    
    private void RaiseEvent(string name, object @event)
    {
    var eventData = new EventData<byte[]>(
        streamId: Id,
        eventId: Guid.NewGuid(),
        name: name,
        sequenceNumber: uncommittedEvents.Count,
        metadata: new Metadata(),
        data: Serialize(@event)
    );
    
    Apply(eventData);
    uncommittedEvents.Add(eventData);
    }
    
    public IEnumerable<EventData<byte[]>> GetUncommittedEvents() => uncommittedEvents;
    
    public void MarkEventsAsCommitted() => uncommittedEvents.Clear();
}

// Usage
var order = Order.Create("order-123");
order.AddItem("product-1", 2);

using var session = store.OpenSession();

    foreach (var eventData in order.GetUncommittedEvents())
    {
    session.Append(0, eventData);
    }
    
    session.SaveChanges();
    order.MarkEventsAsCommitted();
}
```

### Event Position Tracking

Track the last processed position for projections:

```csharp
public class ProjectionCheckpoint
{
    public string Id { get; set; }
    public long Position { get; set; }
    public DateTime LastUpdated { get; set; }
}

// Save checkpoint
using var session = store.OpenSession();

    var checkpoint = session.Load<ProjectionCheckpoint>("order-projection") 
    ?? new ProjectionCheckpoint { Id = "order-projection" };
    
    checkpoint.Position = lastProcessedPosition;
    checkpoint.LastUpdated = DateTime.UtcNow;
    
    session.Store(checkpoint);
    session.SaveChanges();
}

// Load checkpoint
using var session = store.OpenSession();

    var checkpoint = session.Load<ProjectionCheckpoint>("order-projection");
    var fromPosition = checkpoint?.Position ?? 0;
    
    // Process events from this position
}
```

## Best Practices

### 1. Use Meaningful Event Names

```csharp
// Good
name: "OrderCreated"
name: "PaymentProcessed"
name: "ItemShipped"

// Avoid
name: "Event1"
name: "Update"
```

### 2. Include Correlation IDs

```csharp
var metadata = new Metadata
{
    ["CorrelationId"] = correlationId.ToString(),
    ["CausationId"] = causationId.ToString()
};
```

This helps track event chains across aggregates.

### 3. Store Events as Bytes

While HybridDb uses `EventData<byte[]>`, you handle serialization:

```csharp
public static byte[] SerializeEvent<T>(T @event)
{
    var json = JsonConvert.SerializeObject(@event);
    return Encoding.UTF8.GetBytes(json);
}

public static T DeserializeEvent<T>(byte[] data)
{
    var json = Encoding.UTF8.GetString(data);
    return JsonConvert.DeserializeObject<T>(json);
}
```

### 4. Use Snapshots for Long Streams

For aggregates with many events, store periodic snapshots:

```csharp
public class OrderSnapshot
{
    public string Id { get; set; }
    public long EventVersion { get; set; }
    public string Status { get; set; }
    public List<OrderItem> Items { get; set; }
}

// Save snapshot every 100 events
if (eventCount % 100 == 0)
{
    using (var session = store.OpenSession())
    {
    session.Store(new OrderSnapshot 
    { 
        Id = order.Id,
        EventVersion = eventCount,
        Status = order.Status,
        Items = order.Items.ToList()
    });
    session.SaveChanges();
```

### 5. Handle Event Versioning

When event schemas change, handle multiple versions:

```csharp
private void HandleOrderCreated(EventData<byte[]> eventData)
{
    // Check metadata for version
    var version = eventData.Metadata.TryGetValue("Version", out var v) ? v : "1";
    
    switch (version)
    {
    case "1":
        var v1 = DeserializeEvent<OrderCreatedV1>(eventData.Data);
        // Handle V1
        break;
    case "2":
        var v2 = DeserializeEvent<OrderCreatedV2>(eventData.Data);
        // Handle V2
        break;
```

### 6. Use Transactions for Consistency

Ensure events and documents are saved together:

```csharp
using (var tx = store.BeginTransaction())
{
    using var session = store.OpenSession(tx);

        // Append events
    session.Append(0, eventData);
    
    // Update read model
    var order = session.Load<Order>(orderId);
    order.Status = "Shipped";
    session.Store(order);
    
    session.SaveChanges();
    }
    
    tx.Commit();
}
```

## Troubleshooting

### Concurrency Exceptions

If you get frequent concurrency exceptions:
- Use `SequenceNumber.Any` for automatic sequencing
- Implement retry logic with exponential backoff
- Consider using optimistic locking strategies

### Performance Issues

For large event stores:
- Add indexes on StreamId and Position columns
- Use snapshots for aggregates with many events
- Process events in batches
- Consider archiving old events

### Reading Past Active Transactions

Set `readPastActiveTransactions: true` to read events even if there are uncommitted transactions:

```csharp
tx.Execute(new ReadEvents(table, position, readPastActiveTransactions: true))
```

Use with caution as it may read uncommitted data in distributed scenarios.
