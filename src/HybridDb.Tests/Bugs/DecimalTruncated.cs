using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests.Bugs
{
    public class DecimalTruncated : HybridDbTests
    {
        public DecimalTruncated(ITestOutputHelper output) : base(output) { }

        [Fact]
        public void ShouldHaveRightPrecisionAndScale()
        {
            configuration.Document<ClassWithDecimal>().With(x => x.MyDecimal);

            using (var documentSession = store.OpenSession())
            {
                var classWithDecimal = new ClassWithDecimal
                {
                    MyDecimal = 123.456m
                };
                documentSession.Store("id", classWithDecimal);
                documentSession.SaveChanges();
            }

            store.Get(configuration.GetDesignFor<ClassWithDecimal>().Table, "id")["MyDecimal"].ShouldBe(123.456m);
        }

        public class ClassWithDecimal
        {
            public decimal MyDecimal { get; set; }
        }
    }
}