using System;
using System.Linq;
using Xunit;

namespace HybridDb.Tests
{
    public class Class1
    {
        [Fact]
        public void FactMethodName()
        {
            var store = new DocumentStore("Server=.;Initial Catalog=Hybrid;Integrated Security=true");
            store.Document<Asger>().Project(x => x.Mogens.Name);
            store.MigrateSchemaToMatchConfiguration();

            var newGuid = Guid.NewGuid();
            using (var session = store.OpenSession())
            {
                var entity = new Asger {Id = newGuid, Mogens = new Mogens() {Name = "Mogens"}};
                session.Store(entity);
                session.SaveChanges();
            }

            using (var session = store.OpenSession())
            {
                var omgens = session.Load<Asger>(newGuid);
                omgens.Mogens.Name = "Asger";
                session.SaveChanges();
            }
        }
    }

    public class Asger
    {
        public Guid Id { get; set; }
        public Mogens Mogens { get; set; }
    }

    public class Mogens
    {
        public string Name { get; set; }
    }
}