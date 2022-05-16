using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Xunit;
using Shouldly;
using System.Linq;

namespace HybridDb.Tests
{
    public class NullCheckInjectorTests
    {
        [Fact]
        public void AccessThroughNullPropertyToProperty()
        {
            Should.Throw<NullReferenceException>(() => InvokeWithoutNullCheck(x => x.Property.Property));
            InvokeWithNullCheck(x => x.Property.Property).ShouldBe(null);
        }

        [Fact]
        public void AccessThroughNullPropertyToField()
        {
            Should.Throw<NullReferenceException>(() => InvokeWithoutNullCheck(x => x.Property.Field));
            InvokeWithNullCheck(x => x.Property.Field).ShouldBe(null);
        }

        [Fact]
        public void AccessThroughNullPropertyToMethod()
        {
            Should.Throw<NullReferenceException>(() => InvokeWithoutNullCheck(x => x.Property.Method()));
            InvokeWithNullCheck(x => x.Property.Method()).ShouldBe(null);
        }

        [Fact]
        public void AccessThroughNullMethodToProperty()
        {
            Should.Throw<NullReferenceException>(() => InvokeWithoutNullCheck(x => x.Method().Property));
            InvokeWithNullCheck(x => x.Method().Property).ShouldBe(null);
        }

        [Fact]
        public void AccessThroughNullMethodToField()
        {
            Should.Throw<NullReferenceException>(() => InvokeWithoutNullCheck(x => x.Method().Field));
            InvokeWithNullCheck(x => x.Method().Field).ShouldBe(null);
        }

        [Fact]
        public void AccessThroughNullMethodToMethod()
        {
            Should.Throw<NullReferenceException>(() => InvokeWithoutNullCheck(x => x.Method().Method()));
            InvokeWithNullCheck(x => x.Method().Method()).ShouldBe(null);
        }

        [Fact]
        public void AccessThroughNullIndexerToProperty()
        {
            Should.Throw<NullReferenceException>(() => InvokeWithoutNullCheck(x => x.Properties[0].Property));
            InvokeWithNullCheck(x => x.Properties[0].Property).ShouldBe(null);
        }

        [Fact]
        public void AccessToValueTypeThroughNullableTypeReturnsNull()
        {
            Should.Throw<NullReferenceException>(() => InvokeWithoutNullCheck(x => x.Property.NonNullableThingy));
            InvokeWithNullCheck(x => x.Property.NonNullableThingy).ShouldBe(null);
        }

        [Fact]
        public void AccessToValueTypeCanReturnNullIfSpecified()
        {
            Should.Throw<NullReferenceException>(() => InvokeWithoutNullCheck(x => x.Property.NonNullableThingy));
            InvokeWithNullCheck(x => x.Property.NonNullableThingy).ShouldBe(null);
        }

        [Fact]
        public void DirectAccessToValueTypeReturnsValue()
        {
            InvokeWithNullCheck(x => x.NonNullableThingy).ShouldBe(0);
        }

        [Fact]
        public void AccessThroughValueTypeCanReturnNull()
        {
            InvokeWithNullCheck(x => x.Property.NonNullableThingy2.Property).ShouldBe(null);
        }

        [Fact]
        public void MethodCallThroughValueTypeCanReturnNull()
        {
            InvokeWithNullCheck(x => x.NonNullableThingy2.Textualize()).ShouldBe("hej");
        }

        [Fact]
        public void AccessWhereLastMemberIsNullReturnsNull()
        {
            Func<Root> setup = () => new Root {Property = new Root {Field = new Root {Property = new Root()}}};

            Should.NotThrow(() => InvokeWithoutNullCheck(x => x.Property.Field.Property.Property, setup));
            InvokeWithNullCheck(x => x.Property.Field.Property.Property, setup).ShouldBe(null);
        }

        [Fact]
        public void DeepAccessWhereNextToLastMemberIsNull()
        {
            Func<Root> setup = () => new Root { Property = new Root { Field = new Root { Property = new Root() } } };

            Should.Throw<NullReferenceException>(() => InvokeWithoutNullCheck(x => x.Property.Field.Property.Property.Property, setup));
            InvokeWithNullCheck(x => x.Property.Field.Property.Property.Property, setup).ShouldBe(null);
        }

        [Fact]
        public void AccessToExtensionMethodReturningValueType()
        {
            Should.Throw<ArgumentNullException>(() => InvokeWithoutNullCheck(x => x.Properties.Count()));
            InvokeWithNullCheck(x => x.Properties.Count()).ShouldBe(null);
        }

        [Fact]
        public void AccessThrougStaticMethodReturningNullableType()
        {
            Should.Throw<NullReferenceException>(() => InvokeWithoutNullCheck(x => Root.StaticMethod().Property));
            InvokeWithNullCheck(x => Root.StaticMethod().Property).ShouldBe(null);
        }

        [Fact]
        public void CanHandleConstant()
        {
            InvokeWithNullCheck(x => 10).ShouldBe(10);
        }

        [Fact]
        public void CanHandleNullConstant()
        {
            Should.Throw<NullReferenceException>(() => InvokeWithoutNullCheck(x => ((string)null).Length));
            InvokeWithNullCheck(x => ((string)null).Length).ShouldBe(null);
        }

        [Fact]
        public void CanReturnNullConstantAsNullable()
        {
            InvokeWithNullCheck<DateTime?>(x => null).ShouldBe(null);
        }

        [Fact]
        public void StaticMethodReturningValueTypeWillBeTrustedToNeverReturnNull()
        {
            CanItBeTrustedToNeverBeNull(x => Root.StaticMethodReturningValueType()).ShouldBe(true);
        }

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
            var value = "test";
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

        [Fact]
        public void TypeAsOperatorCannotBetrustedToNotReturnNull()
        {
            CanItBeTrustedToNeverBeNull(x => x as OtherRoot).ShouldBe(false);
        }

        [Fact]
        public void CanNullCheckACastExpression()
        {
            InvokeWithNullCheck(x => (ValueType?)x.NonNullableThingy2);
        }

        [Fact]
        public void CanNullCheckATypeAsExpression()
        {
            InvokeWithNullCheck(x => (x.Property as OtherRoot).SpecialThingy, () => new Root {Property = new OtherRoot()}).ShouldBe(0);
        }

        [Fact]
        public void CanNullCheckATypeAsExpressionWhenNotCorrectType()
        {
            InvokeWithNullCheck(x => (x.Property as OtherRoot).SpecialThingy, () => new Root {Property = new UnrelatedOtherRoot()}).ShouldBe(null);
        }

        [Fact]
        public void CanNullCheckATypeIsExpression()
        {
            InvokeWithNullCheck(x => x.Property is OtherRoot, () => new Root { Property = new OtherRoot() }).ShouldBe(true);
        }

        [Fact]
        public void CanNullCheckATypeIsExpressionWhenNotOfType()
        {
            InvokeWithNullCheck(x => x.Property is UnrelatedOtherRoot, () => new Root { Property = new OtherRoot() }).ShouldBe(false);
        }

        [Fact]
        public void CanNullCheckATypeIsExpressionWhenExpressionIsNull()
        {
            InvokeWithNullCheck(x => x.Property is UnrelatedOtherRoot).ShouldBe(false);
        }

        [Fact]
        public void NullCoalescingOperator()
        {
            InvokeWithNullCheck(x => x.Property.NullableInt ?? x.Property.AnotherNullableInt ?? 5).ShouldBe(5);
            InvokeWithNullCheck(x => x.Property.NullableInt ?? x.Property.AnotherNullableInt ?? 5, () => new()
            {
                Property = new Root
                {
                    AnotherNullableInt = 10
                }
            }).ShouldBe(10);

            InvokeWithNullCheck(x => x.Property.NullableInt ?? x.Property.AnotherNullableInt ?? 5, () => new()
            {
                Property = new Root
                {
                    NullableInt = 1,
                    AnotherNullableInt = 10
                }
            }).ShouldBe(1);
        }

        static object InvokeWithoutNullCheck<T>(Expression<Func<Root, T>> exp, Func<Root> setup = null)
        {
            setup = setup ?? (() => new Root());
            return exp.Compile()(setup());
        }

        static object InvokeWithNullCheck<T>(Expression<Func<Root, T>> exp, Func<Root> setup = null)
        {
            setup = setup ?? (() => new Root());
            var nullChecked = new NullCheckInjector().Visit(exp);
            return ((Expression<Func<Root, object>>) nullChecked).Compile()(setup());
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
            public int? NullableInt { get; set; }
            public int? AnotherNullableInt { get; set; }
            public ValueType NonNullableThingy2 { get; set; }
            public ValueType? NullableValueType { get; set; }
            public List<Root> Properties { get; set; }

            public ValueType MethodReturningValueType()
            {
                return default(ValueType);
            }

            public static ValueType StaticMethodReturningValueType()
            {
                return default(ValueType);
            }

            public static Root StaticMethod()
            {
                return null;
            }
        }

        class UnrelatedOtherRoot : Root
        {
            
        }

        class OtherRoot : Root
        {
            public int SpecialThingy { get; set; }
        }

        public struct ValueType
        {
            public Root Property { get; set; }

            public string Textualize()
            {
                return "hej";
            }
        }
    }
}