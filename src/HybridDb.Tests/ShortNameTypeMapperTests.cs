using HybridDb.Config;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class ShortNameTypeMapperTests
    {
        readonly ShortNameTypeMapper typeMapper;

        public ShortNameTypeMapperTests() => 
            typeMapper = new ShortNameTypeMapper();

        [Fact]
        public void SimpleType()
        {
            var discriminator = typeMapper.ToDiscriminator(typeof(MyType));
            var type = typeMapper.ToType(discriminator);

            discriminator.ShouldBe("MyType");
            type.ShouldBe(typeof(MyType));
        }

        [Fact]
        public void NestedType()
        {
            var discriminator = typeMapper.ToDiscriminator(typeof(MyNestedType));
            var type = typeMapper.ToType(discriminator);

            discriminator.ShouldBe("ShortNameTypeMapperTests+MyNestedType");
            type.ShouldBe(typeof(MyNestedType));
        }

        [Fact]
        public void GenericType()
        {
            var discriminator = typeMapper.ToDiscriminator(typeof(MyGenericType<int>));
            var type = typeMapper.ToType(discriminator);

            discriminator.ShouldBe("MyGenericType`1(Int32)");
            type.ShouldBe(typeof(MyGenericType<int>));
        }

        [Fact]
        public void AnonType()
        {
            var x = new { MyString = "Asger" };
            var discriminator = typeMapper.ToDiscriminator(x.GetType());
            var type = typeMapper.ToType(discriminator);

            type.ShouldBe(x.GetType());
        }

        [Fact]
        public void GenericType_MultipleArguments()
        {
            var discriminator = typeMapper.ToDiscriminator(typeof(MyGenericType<int, string>));
            var type = typeMapper.ToType(discriminator);

            discriminator.ShouldBe("MyGenericType`2(Int32|String)");
            type.ShouldBe(typeof(MyGenericType<int, string>));
        }

        [Fact]
        public void GenericType_NestedArguments()
        {
            var discriminator = typeMapper.ToDiscriminator(typeof(MyGenericType<MyNestedType>));
            var type = typeMapper.ToType(discriminator);

            discriminator.ShouldBe("MyGenericType`1(ShortNameTypeMapperTests+MyNestedType)");
            type.ShouldBe(typeof(MyGenericType<MyNestedType>));
        }

        [Fact]
        public void GenericType_GenericArguments()
        {
            var discriminator = typeMapper.ToDiscriminator(typeof(MyGenericType<MyGenericType<string>, string>));
            var type = typeMapper.ToType(discriminator);

            discriminator.ShouldBe("MyGenericType`2(MyGenericType`1(String)|String)");
            type.ShouldBe(typeof(MyGenericType<MyGenericType<string>, string>));
        }        
        
        [Fact]
        public void GenericType_MultipleGenericArguments()
        {
            var discriminator = typeMapper.ToDiscriminator(typeof(MyGenericType<MyGenericType<string>, (MyType, bool)>));
            var type = typeMapper.ToType(discriminator);

            discriminator.ShouldBe("MyGenericType`2(MyGenericType`1(String)|ValueTuple`2(MyType|Boolean))");
            type.ShouldBe(typeof(MyGenericType<MyGenericType<string>, (MyType, bool)>));
        }

        public class MyNestedType { }
    }

    public class MyType { }
    public class MyGenericType<T1> { }
    public class MyGenericType<T1, T2> { }
}