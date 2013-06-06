using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Xunit;
using Shouldly;

namespace HybridDb.Tests
{
    public class NullCheckInjectorTests
    {
        readonly Root value;

        public NullCheckInjectorTests()
        {
            value = new Root();
        }

        [Fact]
        public void AccessThroughNullPropertyToProperty()
        {
            Should.Throw<NullReferenceException>(() => InvokeWithoutNullCheck(() => value.Property.Property));
            InvokeWithNullCheck(() => value.Property.Property).ShouldBe(null);
        }

        [Fact]
        public void AccessThroughNullPropertyToField()
        {
            Should.Throw<NullReferenceException>(() => InvokeWithoutNullCheck(() => value.Property.Field));
            InvokeWithNullCheck(() => value.Property.Field).ShouldBe(null);
        }

        [Fact]
        public void AccessThroughNullPropertyToMethod()
        {
            Should.Throw<NullReferenceException>(() => InvokeWithoutNullCheck(() => value.Property.Method()));
            InvokeWithNullCheck(() => value.Property.Method()).ShouldBe(null);
        }

        [Fact]
        public void AccessThroughNullMethodToProperty()
        {
            Should.Throw<NullReferenceException>(() => InvokeWithoutNullCheck(() => value.Method().Property));
            InvokeWithNullCheck(() => value.Method().Property).ShouldBe(null);
        }

        [Fact]
        public void AccessThroughNullMethodToField()
        {
            Should.Throw<NullReferenceException>(() => InvokeWithoutNullCheck(() => value.Method().Field));
            InvokeWithNullCheck(() => value.Method().Field).ShouldBe(null);
        }

        [Fact]
        public void AccessThroughNullMethodToMethod()
        {
            Should.Throw<NullReferenceException>(() => InvokeWithoutNullCheck(() => value.Method().Method()));
            InvokeWithNullCheck(() => value.Method().Method()).ShouldBe(null);
        }

        [Fact]
        public void AccessThroughNullIndexerToProperty()
        {
            Should.Throw<NullReferenceException>(() => InvokeWithoutNullCheck(() => value.Properties[0].Property));
            InvokeWithNullCheck(() => value.Properties[0].Property).ShouldBe(null);
        }

        [Fact]
        public void AccessReturnsDefaultValueIfSpecified()
        {
            Should.Throw<NullReferenceException>(() => InvokeWithoutNullCheck(() => value.Property.NonNullableThingy));
            InvokeWithNullCheck(() => value.Property.NonNullableThingy, defaultToNull: false).ShouldBe(0);
        }

        [Fact]
        public void AccessReturnsNullIfSpecified()
        {
            Should.Throw<NullReferenceException>(() => InvokeWithoutNullCheck(() => value.Property.NonNullableThingy));
            InvokeWithNullCheck(() => value.Property.NonNullableThingy, defaultToNull: true).ShouldBe(null);
        }

        [Fact]
        public void AccessReturnsDefaultIfColumnIsNonNullable()
        {
            InvokeWithNullCheck(() => value.NonNullableThingy2.Property.NonNullableThingy2.Property, defaultToNull: false).ShouldBe(null);
        }

        [Fact]
        public void AccessWhereLastMemberIsNullReturnsNull()
        {
            value.Property = new Root { Field = new Root { Property = new Root() } };

            Should.NotThrow(() => InvokeWithoutNullCheck(() => value.Property.Field.Property.Property));
            InvokeWithNullCheck(() => value.Property.Field.Property.Property).ShouldBe(null);
        }

        [Fact]
        public void DeepAccessWhereNextToLastMemberIsNull()
        {
            value.Property = new Root { Field = new Root { Property = new Root() } };

            Should.Throw<NullReferenceException>(() => InvokeWithoutNullCheck(() => value.Property.Field.Property.Property.Property));
            InvokeWithNullCheck(() => value.Property.Field.Property.Property.Property).ShouldBe(null);
        }

        static object InvokeWithoutNullCheck(Expression<Func<object>> exp)
        {
            return exp.Compile()();
        }

        static object InvokeWithNullCheck<T>(Expression<Func<T>> exp, bool defaultToNull = true)
        {
            var nullChecked = new NullCheckInjector(defaultToNull).Visit(exp);
            return ((Expression<Func<object>>) nullChecked).Compile()();
        }

        public class Root
        {
            public Root Property { get; set; }
            public Root Field;
            public Root Method()
            {
                return null;
            }

            public int NonNullableThingy { get; set; }
            public ValueType NonNullableThingy2 { get; set; }
            public List<Root> Properties { get; set; }
        }

        public struct ValueType
        {
            public Root Property { get; set; }
        }
    }
}