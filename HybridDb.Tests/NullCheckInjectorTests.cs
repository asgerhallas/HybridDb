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
        public void AccessToValueTypeThroughNullableTypeReturnsNull()
        {
            Should.Throw<NullReferenceException>(() => InvokeWithoutNullCheck(() => value.Property.NonNullableThingy));
            InvokeWithNullCheck(() => value.Property.NonNullableThingy).ShouldBe(null);
        }

        [Fact]
        public void AccessToValueTypeCanReturnNullIfSpecified()
        {
            Should.Throw<NullReferenceException>(() => InvokeWithoutNullCheck(() => value.Property.NonNullableThingy));
            InvokeWithNullCheck(() => value.Property.NonNullableThingy).ShouldBe(null);
        }

        [Fact]
        public void DirectAccessToValueTypeReturnsValue()
        {
            InvokeWithNullCheck(() => value.NonNullableThingy).ShouldBe(0);
        }

        [Fact]
        public void AccessThroughValueTypeCanReturnNull()
        {
            InvokeWithNullCheck(() => value.Property.NonNullableThingy2.Property).ShouldBe(null);
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

        //[Fact]
        //public void AccessThroughNullMethodToMethod()
        //{
        //    Should.Throw<NullReferenceException>(() => InvokeWithoutNullCheck(() => value.Method().Method()));
        //    InvokeWithNullCheck(() => value.Method().Method()).ShouldBe(null);
        //}

        [Fact]
        public void NullableValueTypesWillNotBeTrustedToNeverReturnNull()
        {
            CanItBeTrustedToNeverBeNull(x => x.NullableValueType).ShouldBe(false);
        }

        [Fact]
        public void MethodReturningReferenceTypesWillNotBeTrustedToNeverReturnNull()
        {
            CanItBeTrustedToNeverBeNull(x => x.Method()).ShouldBe(false);
        }

        [Fact]
        public void MethodReturningValueTypesWillBeTrustedToNeverReturnNull()
        {
            CanItBeTrustedToNeverBeNull(x => x.MethodReturningValueType()).ShouldBe(true);
        }

        [Fact]
        public void ClosedVariablesWillNotBeTrustedToNeverReturnNull()
        {
            CanItBeTrustedToNeverBeNull(x => value).ShouldBe(false);
        }

        [Fact]
        public void ParametersWillBeTrustedToNeverReturnNull()
        {
            CanItBeTrustedToNeverBeNull(x => x).ShouldBe(true);
        }

        [Fact]
        public void AllValueTypesWillBeTrustedToNeverReturnNull()
        {
            CanItBeTrustedToNeverBeNull(x => x.NonNullableThingy).ShouldBe(true);
        }

        [Fact]
        public void AllValueTypesMixedWithReferenceTypesWillNotBeTrustedToNeverReturnNull()
        {
            CanItBeTrustedToNeverBeNull(x => x.Property.NonNullableThingy).ShouldBe(false);
        }

        static object InvokeWithoutNullCheck(Expression<Func<object>> exp)
        {
            return exp.Compile()();
        }

        static object InvokeWithNullCheck<T>(Expression<Func<T>> exp)
        {
            var nullChecked = new NullCheckInjector().Visit(exp);
            return ((Expression<Func<object>>) nullChecked).Compile()();
        }

        static bool CanItBeTrustedToNeverBeNull<T>(Expression<Func<Root, T>> exp)
        {
            var nullCheckInjector = new NullCheckInjector();
            nullCheckInjector.Visit(exp);
            return nullCheckInjector.CanBeTrustedToNeverReturnNull;
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
            public ValueType? NullableValueType { get; set; }
            public List<Root> Properties { get; set; }

            public ValueType MethodReturningValueType()
            {
                return default(ValueType);
            }
        }

        public struct ValueType
        {
            public Root Property { get; set; }
        }
    }
}