using System;
using HybridDb.Migrations.Documents;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using static HybridDb.Helpers;

namespace HybridDb.Tests
{
    public class DocumentSession_ReadOnlyTests(ITestOutputHelper output) : HybridDbTests(output)
    {
        [Fact]
        public void ReadOnly_Modify()
        {
            Document<Entity>();

            var id = NewId();

            using var session = store.OpenSession();
            session.Store(id, new Entity
            {
                Field = "Lars"
            });

            session.SaveChanges();
            session.Advanced.Clear();

            var readOnlyEntity = session.Load<Entity>(id, readOnly: true);
            var readOnlyEtag = session.Advanced.GetEtagFor(readOnlyEntity);
            
            readOnlyEntity.Field = "Asger";

            session.SaveChanges();
            session.Advanced.Clear();

            var finalEntity = session.Load<Entity>(id);
            finalEntity.Field.ShouldBe("Lars");
            session.Advanced.GetEtagFor(finalEntity).ShouldBe(readOnlyEtag);
        }

        [Fact]
        public void ReadOnly_Delete()
        {
            Document<Entity>();

            var id = NewId();

            using var session = store.OpenSession();
            session.Store(id, new Entity
            {
                Field = "Lars"
            });

            session.SaveChanges();
            session.Advanced.Clear();

            var readOnlyEntity = session.Load<Entity>(id, readOnly: true);

            session.Delete(readOnlyEntity);

            session.SaveChanges();
            session.Advanced.Clear();

            session.Load<Entity>(id).ShouldNotBe(null);
        }

        [Fact]
        public void ReadOnly_Store()
        {
            Document<Entity>();

            var id = NewId();

            using var session = store.OpenSession();
            var entity = new Entity
            {
                Field = "Lars"
            };

            session.Store(id, entity);

            session.Advanced.ManagedEntities.TryGetValue(entity, out var managedEntity);
            managedEntity.ReadOnly = true;

            session.SaveChanges();
            session.Advanced.Clear();

            session.Load<Entity>(id).ShouldBe(null);
        }

        [Fact]
        public void ReadOnly_NotAtomic()
        {
            Document<Entity>();

            var id1 = NewId();
            var id2 = NewId();

            using var session = store.OpenSession();
            session.Store(id1, new Entity
            {
                Field = "Lars"
            });

            session.Store(id2, new Entity
            {
                Field = "Asger"
            });

            session.SaveChanges();
            session.Advanced.Clear();

            var readOnlyEntity = session.Load<Entity>(id1, readOnly: true);
            var writableEntity = session.Load<Entity>(id2, readOnly: false);

            readOnlyEntity.Field = "Peter";
            writableEntity.Field = "Jacob";

            session.SaveChanges();
            session.Advanced.Clear();

            session.Load<Entity>(id1).Field.ShouldBe("Lars");
            session.Load<Entity>(id2).Field.ShouldBe("Jacob");
        }

        [Fact]
        public void Load_Then_LoadReadOnly()
        {
            Document<Entity>();

            var id = NewId();

            using var session = store.OpenSession();
            session.Store(id, new Entity());

            session.SaveChanges();
            session.Advanced.Clear();

            session.Load<Entity>(id);

            Should.Throw<InvalidOperationException>(() => session.Load<Entity>(id, readOnly: true))
                .Message.ShouldBe("Document can not be loaded as readonly, as it is already loaded or stored in session as writable.");
        }        
        
        [Fact]
        public void Store_Then_LoadReadOnly()
        {
            Document<Entity>();

            var id = NewId();

            using var session = store.OpenSession();
            session.Store(id, new Entity());

            Should.Throw<InvalidOperationException>(() => session.Load<Entity>(id, readOnly: true))
                .Message.ShouldBe("Document can not be loaded as readonly, as it is already loaded or stored in session as writable.");
        }

        [Fact]
        public void LoadReadOnly_Then_Load()
        {
            Document<Entity>();

            var id = NewId();

            using var session = store.OpenSession();
            session.Store(id, new Entity());

            session.SaveChanges();
            session.Advanced.Clear();

            session.Load<Entity>(id, readOnly: true);
            var entity = session.Load<Entity>(id);

            session.Advanced.ManagedEntities.TryGetValue(entity, out var managedEntity);
            managedEntity.ReadOnly.ShouldBe(true);
        }

        [Fact]
        public void DeleteMigration_LoadReadOnly()
        {
            Document<Entity>();

            var id = NewId();

            using var session1 = store.OpenSession();
            session1.Store(id, new Entity());

            session1.SaveChanges();
            session1.Advanced.Clear();

            ResetConfiguration();

            Document<Entity>();
            UseMigrations(new InlineMigration(1, new DeleteDocuments<Entity>()));

            using var session2 = store.OpenSession();

            session2.Load<Entity>(id, readOnly: true).ShouldBe(null);

            session2.SaveChanges();
            session2.Advanced.Clear();

            session2.Advanced.Exists<Entity>(id, out var etag).ShouldBe(false);
        }
    }
}