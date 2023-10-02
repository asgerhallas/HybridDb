using System;
using HybridDb.Config;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class ShortNameTypeMapperTests
    {
        readonly ShortNameTypeMapper typeMapper = new(typeof(ShortNameTypeMapperTests).Assembly);

        [Fact]
        public void SimpleType()
        {
            var discriminator = typeMapper.ToDiscriminator(typeof(MyType));
            var type = typeMapper.ToType(typeof(object), discriminator);

            discriminator.ShouldBe("MyType");
            type.ShouldBe(typeof(MyType));
        }

        [Fact]
        public void SimpleType_MustBeBaseType()
        {
            var discriminator = typeMapper.ToDiscriminator(typeof(MyType));
            Should.Throw<InvalidOperationException>(() => typeMapper.ToType(typeof(NotABaseType), discriminator))
                .Message.ShouldBe("No type found for 'MyType'.");
        }

        [Fact]
        public void SimpleType_ChooseByBaseType()
        {
            var discriminator = typeMapper.ToDiscriminator(typeof(MyOtherNamespace.TwinType));
            var type = typeMapper.ToType(typeof(ABaseType), discriminator);

            discriminator.ShouldBe("TwinType");
            type.ShouldBe(typeof(MyOtherNamespace.TwinType));
        }

        [Fact]
        public void NestedType()
        {
            var discriminator = typeMapper.ToDiscriminator(typeof(MyNestedType));
            var type = typeMapper.ToType(typeof(object), discriminator);

            discriminator.ShouldBe("ShortNameTypeMapperTests+MyNestedType");
            type.ShouldBe(typeof(MyNestedType));
        }

        [Fact]
        public void GenericType()
        {
            var discriminator = typeMapper.ToDiscriminator(typeof(MyGenericType<int>));
            var type = typeMapper.ToType(typeof(object), discriminator);

            discriminator.ShouldBe("MyGenericType`1(Int32)");
            type.ShouldBe(typeof(MyGenericType<int>));
        }

        [Fact]
        public void AnonType()
        {
            var x = new {MyString = "Asger"};
            var discriminator = typeMapper.ToDiscriminator(x.GetType());
            var type = typeMapper.ToType(typeof(object), discriminator);

            type.ShouldBe(x.GetType());
        }

        [Fact]
        public void GenericType_MultipleArguments()
        {
            var discriminator = typeMapper.ToDiscriminator(typeof(MyGenericType<int, string>));
            var type = typeMapper.ToType(typeof(object), discriminator);

            discriminator.ShouldBe("MyGenericType`2(Int32|String)");
            type.ShouldBe(typeof(MyGenericType<int, string>));
        }

        [Fact]
        public void GenericType_NestedArguments()
        {
            var discriminator = typeMapper.ToDiscriminator(typeof(MyGenericType<MyNestedType>));
            var type = typeMapper.ToType(typeof(object), discriminator);

            discriminator.ShouldBe("MyGenericType`1(ShortNameTypeMapperTests+MyNestedType)");
            type.ShouldBe(typeof(MyGenericType<MyNestedType>));
        }

        [Fact]
        public void GenericType_GenericArguments()
        {
            var discriminator = typeMapper.ToDiscriminator(typeof(MyGenericType<MyGenericType<string>, string>));
            var type = typeMapper.ToType(typeof(object), discriminator);

            discriminator.ShouldBe("MyGenericType`2(MyGenericType`1(String)|String)");
            type.ShouldBe(typeof(MyGenericType<MyGenericType<string>, string>));
        }

        [Fact]
        public void GenericType_MultipleGenericArguments()
        {
            var discriminator = typeMapper.ToDiscriminator(typeof(MyGenericType<MyGenericType<string>, (MyType, bool)>));
            var type = typeMapper.ToType(typeof(object), discriminator);

            discriminator.ShouldBe("MyGenericType`2(MyGenericType`1(String)|ValueTuple`2(MyType|Boolean))");
            type.ShouldBe(typeof(MyGenericType<MyGenericType<string>, (MyType, bool)>));
        }

        [Fact]
        public void UnknownAssembly() =>
            Should.Throw<InvalidOperationException>(() => typeMapper.ToDiscriminator(typeof(FactAttribute)))
                .Message.ShouldBe("Type 'Xunit.FactAttribute' cannot get a shortname discriminator as the assembly is not known to HybridDb. Only assemblies of types that are configured with configuration.Document<T>(), CoreLib and the assemblies in which the DocumentStore are instantiated are known by default. Please add a call to `configuration.UseTypeMapper(new ShortNameTypeMapper(typeof(FactAttribute).Assembly));` or 'configuration.TypeMapper.Add(typeof(FactAttribute).Assembly);' to your HybridDb configuration.");

        public class MyNestedType { }
    }

    public class ABaseType { }
    public class NotABaseType { }

    public class MyType { }
    public class TwinType { }

    public class MyGenericType<T1> { }

    public class MyGenericType<T1, T2> { }

    namespace MyOtherNamespace
    {
        public class TwinType : ABaseType { }
    }
}