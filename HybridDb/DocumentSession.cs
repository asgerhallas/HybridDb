using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HybridDb
{
    public class DocumentSession : IDocumentSession
    {
        readonly AdvancedDocumentSessionCommands advanced;
        readonly Dictionary<Guid, ManagedEntity> entities;
        readonly IDocumentStore store;

        public DocumentSession(IDocumentStore store)
        {
            entities = new Dictionary<Guid, ManagedEntity>();
            this.store = store;

            advanced = new AdvancedDocumentSessionCommands(this);
        }

        public IAdvancedDocumentSessionCommands Advanced
        {
            get { return advanced; }
        }

        public T Load<T>(Guid id) where T : class
        {
            ManagedEntity managedEntity;
            if (entities.TryGetValue(id, out managedEntity))
            {
                return managedEntity.State != EntityState.Deleted
                           ? (T) managedEntity.Entity
                           : null;
            }

            var table = store.Configuration.GetTableFor<T>();
            var row = store.Get(table, id);
            if (row == null)
                return null;

            var entity = ConvertToEntityAndPutUnderManagement<T>(table, row);
            return entity;
        }

        public IEnumerable<T> Query<T>(string where, object parameters) where T : class
        {
            var table = store.Configuration.GetTableFor<T>();
            var columns = string.Join(",", new[] {table.IdColumn.Name, table.EtagColumn.Name, table.DocumentColumn.Name});
            int totalRows;
            var rows = store.Query(table, out totalRows, columns, where, 0, 0, parameters);

            return rows.Select(row => ConvertToEntityAndPutUnderManagement<T>(table, row))
                       .Where(entity => entity != null);
        }

        public IEnumerable<TProjection> Query<T, TProjection>(string where, object parameters) where T : class
        {
            var table = store.Configuration.GetTableFor<T>();
            var properties = typeof (TProjection).GetProperties();
            var columns = string.Join(",", properties.Select(x => x.Name));
            int totalRows;
            var rows = store.Query<TProjection>(table, out totalRows, columns, where, 0, 0, parameters);
            return rows;
        }

        public void Store(object entity)
        {
            var table = store.Configuration.GetTableFor(entity.GetType());
            var id = (Guid) table.IdColumn.GetValue(entity);
            if (entities.ContainsKey(id))
                return;

            entities.Add(id, new ManagedEntity
            {
                Key = id,
                Entity = entity,
                State = EntityState.Transient
            });
        }

        public void Delete(object entity)
        {
            var table = store.Configuration.GetTableFor(entity.GetType());
            var id = (Guid) table.IdColumn.GetValue(entity);

            ManagedEntity managedEntity;
            if (!entities.TryGetValue(id, out managedEntity))
                return;

            if (managedEntity.State == EntityState.Transient)
            {
                entities.Remove(id);
            }
            else
            {
                entities[id].State = EntityState.Deleted;
            }
        }

        public void SaveChanges()
        {
            var serializer = store.Configuration.Serializer;

            var commands = new Dictionary<ManagedEntity, DatabaseCommand>();
            foreach (var managedEntity in entities.Values)
            {
                var id = managedEntity.Key;
                var table = store.Configuration.GetTableFor(managedEntity.Entity.GetType());
                var projections = table.Columns.OfType<IProjectionColumn>().ToDictionary(x => x.Name, x => x.GetValue(managedEntity.Entity));
                var document = serializer.Serialize(managedEntity.Entity);

                switch (managedEntity.State)
                {
                    case EntityState.Transient:
                        commands.Add(managedEntity, new InsertCommand(table, id, document, projections));
                        break;
                    case EntityState.Loaded:
                        var a = Encoding.UTF8.GetString(managedEntity.Document);
                        var b = Encoding.UTF8.GetString(document);
                        if (!managedEntity.Document.SequenceEqual(document))
                            commands.Add(managedEntity, new UpdateCommand(table, id, managedEntity.Etag, document, projections));
                        break;
                    case EntityState.Deleted:
                        commands.Add(managedEntity, new DeleteCommand(table, id, managedEntity.Etag));
                        break;
                }
            }

            if (commands.Count == 0)
                return;

            var etag = store.Execute(commands.Values.ToArray());

            foreach (var change in commands)
            {
                var managedEntity = change.Key;
                var command = change.Value;

                var insertCommand = command as InsertCommand;
                if (insertCommand != null)
                {
                    managedEntity.State = EntityState.Loaded;
                    managedEntity.Etag = etag;
                    managedEntity.Document = insertCommand.Document;
                    continue;
                }

                var updateCommand = command as UpdateCommand;
                if (updateCommand != null)
                {
                    managedEntity.Etag = etag;
                    managedEntity.Document = updateCommand.Document;
                    continue;
                }

                if (command is DeleteCommand)
                {
                    entities.Remove(managedEntity.Key);
                }
            }
        }

        public void Dispose() {}

        T ConvertToEntityAndPutUnderManagement<T>(ITable table, IDictionary<IColumn, object> row) where T : class
        {
            var id = (Guid) row[table.IdColumn];

            ManagedEntity managedEntity;
            if (entities.TryGetValue(id, out managedEntity))
            {
                return managedEntity.State != EntityState.Deleted
                           ? (T) managedEntity.Entity
                           : null;
            }

            var document = (byte[]) row[table.DocumentColumn];
            var entity = store.Configuration.Serializer.Deserialize<T>(document);

            managedEntity = new ManagedEntity
            {
                Key = id,
                Entity = entity,
                Etag = (Guid) row[table.EtagColumn],
                State = EntityState.Loaded,
                Document = document
            };

            entities.Add(id, managedEntity);
            return entity;
        }

        class AdvancedDocumentSessionCommands : IAdvancedDocumentSessionCommands
        {
            readonly DocumentSession session;

            public AdvancedDocumentSessionCommands(DocumentSession session)
            {
                this.session = session;
            }

            public void Clear()
            {
                session.entities.Clear();
            }

            public bool IsLoaded(Guid id)
            {
                return session.entities.ContainsKey(id);
            }
        }

        enum EntityState
        {
            Transient,
            Loaded,
            Deleted
        }

        class ManagedEntity
        {
            public object Entity { get; set; }
            public Guid Key { get; set; }
            public Guid Etag { get; set; }
            public EntityState State { get; set; }
            public byte[] Document { get; set; }
        }
    }
}