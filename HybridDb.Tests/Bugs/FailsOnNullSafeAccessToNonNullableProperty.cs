using System;
using System.Linq.Expressions;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Bugs
{
    public class NullSafeAccessToNonNullableProperty
    {
        [Fact]
        public void Fails()
        {
            Expression<Func<Case, long>> test = x => x.Submission.EnergyLabelSerialIdentifier;
            var expression = new NullCheckInjector().Visit(test);
            var nullsafe = (Expression<Func<Case, object>>)expression;
            Should.NotThrow(() => nullsafe.Compile()(new Case()));
        }

        public class Case
        {
            public Submission Submission { get; set; }
        }

        public class Submission
        {
            public long EnergyLabelSerialIdentifier { get; set; }
        }
    }
}