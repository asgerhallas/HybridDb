using System;
using System.Linq.Expressions;
using HybridDb.Linq.Compilers;
using ShouldBeLike;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Linq.Compilers
{
    public class PreProcessors_ReduceConstantMemberAccessTests
    {
        [Fact]
        public void ReduceConstantMemberAccess()
        {
            var test = new A
                {
                Property = new A
                {
                    Property = new A()
                }
            };

            var expression = PreProcessors.ReduceConstantMemberAccess((Expression<Func<A>>) (
                () => test.Property.Property
            ));

            // Assertion is like this, because of this: https://stackoverflow.com/questions/60452684/net-core-and-type-equality
            (expression as Expression<Func<A>>)
                .Body.ShouldBeOfType<ConstantExpression>()
                .Value.ShouldBe(test.Property.Property);
        }

        [Fact]
        public void SkipNonConstantMemberAccess()
        {
            var input = (Expression<Func<A, A>>) (x => x.Property);

            var output = PreProcessors.ReduceConstantMemberAccess(input);

            output.ShouldBeLike(input);
        }

        [Fact]
        public void SkipChainedNonConstantMemberAccess()
        {
            var input = (Expression<Func<A, A>>) (x => x.Property.Property);

            var output = PreProcessors.ReduceConstantMemberAccess(input);

            output.ShouldBeLike(input);
        }

        public class A
        {
            public A Property { get; set; }
        }
    }
}