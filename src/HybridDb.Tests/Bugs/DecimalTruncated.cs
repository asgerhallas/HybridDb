using Shouldly;
using Xunit;

namespace HybridDb.Tests.Bugs
{
    public class DecimalTruncated : HybridDbTests
    { 
        [Fact]
        public void ShouldHaveRightPrecisionAndScale()
        {
            documentStore.Configuration.Document<ClassWithDecimal>().With(x => x.MyDecimal);
            documentStore.Initialize();

            using (var documentSession = documentStore.OpenSession())
            {
                var classWithDecimal = new ClassWithDecimal
                {
                    MyDecimal = 123.456m
                };
                documentSession.Store("id", classWithDecimal);
                documentSession.SaveChanges();
            }

            documentStore.Get(documentStore.Configuration.GetDesignFor<ClassWithDecimal>().Table, "id")["MyDecimal"].ShouldBe(123.456m);
        }

        public class ClassWithDecimal
        {
            public decimal MyDecimal { get; set; }
        }
    }
}