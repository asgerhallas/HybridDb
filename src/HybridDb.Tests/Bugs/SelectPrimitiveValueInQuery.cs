using System;
using System.Linq;
using System.Threading.Tasks;
using HybridDb.Linq;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Bugs
{
    public class SelectPrimitiveValueInQuery : HybridDbAutoInitializeTests
    {
        [Fact]
        public async Task ShouldBeAble()
        {
            Document<WeirdEntity>()
                .With(x => x.A)
                .With(x => x.B)
                .With(x => x.C)
                .With(x => x.D);

            using (var session = store.OpenSession())
            {
                var id = Guid.NewGuid();
                session.Store(new WeirdEntity
                {
                    Id = id,
                    A = id,
                    B = "asger",
                    C = DateTime.MaxValue,
                    D = null
                });
                await session.SaveChanges();
                session.Advanced.Clear();

                session.Query<WeirdEntity>().Select(x => x.Column<string>("Id")).ToList().Single().ShouldBe(id.ToString());
                session.Query<WeirdEntity>().Select(x => x.A).ToList().Single().ShouldBe(id);
                session.Query<WeirdEntity>().Select(x => x.B).ToList().Single().ShouldBe("asger");
                session.Query<WeirdEntity>().Select(x => x.C).ToList().Single().ShouldBe(DateTime.MaxValue);
                session.Query<WeirdEntity>().Select(x => x.D).ToList().Single().ShouldBe(null);
            }
        }

        public class WeirdEntity
        {
            public Guid Id { get; set; }
            public Guid? A { get; set; }
            public string B { get; set; }
            public DateTime C { get; set; }
            public DateTime? D { get; set; }
        }
    }
}