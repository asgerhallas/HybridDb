using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Bugs
{
    public class DecimalTruncated : HybridDbTests
    { 
        [Fact]
        public async Task ShouldHaveRightPrecisionAndScale()
        {
            store.Configuration.Document<ClassWithDecimal>().With(x => x.MyDecimal);
            store.Initialize();

            using (var documentSession = store.OpenSession())
            {
                var classWithDecimal = new ClassWithDecimal
                {
                    MyDecimal = 123.456m
                };
                documentSession.Store("id", classWithDecimal);
                await documentSession.SaveChanges();
            }

            (await store.Get(store.Configuration.GetDesignFor<ClassWithDecimal>().Table, "id"))["MyDecimal"].ShouldBe(123.456m);
        }

        public class ClassWithDecimal
        {
            public decimal MyDecimal { get; set; }
        }
    }
}