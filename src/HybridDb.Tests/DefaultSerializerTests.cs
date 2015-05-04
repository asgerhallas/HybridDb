using System;
using System.Linq;
using System.Collections.Generic;
using HybridDb.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class DefaultSerializerTests
    {
        readonly DefaultSerializer serializer;

        public DefaultSerializerTests()
        {
            serializer = new DefaultSerializer();
        }

        JsonSerializer CreateSerializer()
        {
            return serializer.CreateSerializer();
        }

        [Fact]
        public void RoundtripsAutoPropertiesAndFieldsEvenPrivates()
        {
            var jObject = JObject.FromObject(new WithSomePrivates(), CreateSerializer());

            jObject.ShouldNotContainKey("privateField");
            jObject.ShouldContainKeyAndValue("PrivateField", "lars");

            jObject.ShouldNotContainKey("field");
            jObject.ShouldContainKeyAndValue("Field", "asger");

            jObject.ShouldContainKeyAndValue("PrivateSetProperty", 42);
        }

        [Fact]
        public void PropertiesAreNotActuallySerialized()
        {
            Should.NotThrow(() => JObject.FromObject(new WithThrowingProperty(), CreateSerializer()));
        }

        [Fact]
        public void RoundTripsPropertyBackingField()
        {
            var original = new WithPropertyWithBackingField
            {
                propertyWithBackingField = 5
            };

            var jObject = JObject.FromObject(original, CreateSerializer());

            jObject.ShouldNotContainKey("propertyWithBackingField");
            jObject.ShouldContainKeyAndValue("PropertyWithBackingField", 5);

            var copy = jObject.ToObject<WithPropertyWithBackingField>(CreateSerializer());

            copy.propertyWithBackingField.ShouldBe(5);
            copy.PropertyWithBackingField.ShouldBe(10);
        }

        [Fact]
        public void RoundtripsAnonymousTypes()
        {
            var jObject = JObject.FromObject(new { String = "asger" }, CreateSerializer());
            jObject.ShouldContainKeyAndValue("String", "asger");

            var copy = jObject.ToObject<dynamic>();
            var s = (string) copy.String;
            s.ShouldBe("asger");
        }

        [Fact]
        public void DoesNotInvokeCtorOnDeserialize()
        {
            var original = new WithMutatingCtor
            {
                Value = "SET BY TEST"
            };

            var copy = JObject.FromObject(original, CreateSerializer()).ToObject<WithMutatingCtor>();

            copy.Value.ShouldBe("SET BY TEST");
        }

        [Fact]
        public void SkipsEvents()
        {
            var original = new WithEvent();
            original.eventhandler += (sender, args) => { };
            original.action += () => { };
            original.func += () => 1;

            var jObject = JObject.FromObject(original, CreateSerializer());
            
            jObject.ShouldNotContainKey("eventhandler");
            jObject.ShouldNotContainKey("action");
            jObject.ShouldNotContainKey("func");

            var copy = jObject.ToObject<WithEvent>();
            copy.GetEventHandler().ShouldBe(null);
            copy.action.ShouldBe(null);
            copy.func.ShouldBe(null);
        }

        [Fact]
        public void OrdersProperties()
        {
            var original = new WithChaos();

            var jObject = JObject.FromObject(original, CreateSerializer()).Properties().Select(x => x.Name).ToList();

            jObject[0].ShouldBe("$id");
            jObject[1].ShouldBe("Id");
            jObject[2].ShouldBe("Number");
            jObject[3].ShouldBe("String");
            jObject[4].ShouldBe("List");
        }

        [Fact]
        public void CanAddSpecialOrder()
        {
            var original = new WithChaos();

            serializer.Order(0, property => property.PropertyName == "List");

            var jObject = JObject.FromObject(original, CreateSerializer()).Properties().Select(x => x.Name).ToList();

            jObject[0].ShouldBe("$id");
            jObject[1].ShouldBe("List");
            jObject[2].ShouldBe("Id");
            jObject[3].ShouldBe("Number");
            jObject[4].ShouldBe("String");
        }

        [Fact]
        public void CanAddConverter()
        {
            serializer.AddConverter(new StringToStringLengthConverter());

            var jObject = JObject.FromObject(new { String = "asger" }, CreateSerializer());

            jObject.ShouldContainKeyAndValue("String", 5);
        }

        [Fact]
        public void DisablesPreserveReferenceHandling()
        {
            serializer.EnableAutomaticBackReferences();

            var jObject = JObject.FromObject(new { String = "asger" }, CreateSerializer());

            jObject.ShouldNotContainKey("$id");
            jObject.ShouldContainKeyAndValue("String", "asger");
        }

        [Fact]
        public void ParentReferencesAreNotSerializedButAutomaticallyHydrated()
        {
            serializer.EnableAutomaticBackReferences();

            var root = new Parent();
            var child = new Child
            {
                Parent = root
            };

            child.GrandChild = new Child
            {
                Root = root,
                Parent = child
            };

            root.Child = child;

            var jObject = JObject.FromObject(root, CreateSerializer());

            var jChild = ((JObject)jObject["Child"]);
            jChild.ShouldContainKeyAndValue("Root", JValue.CreateNull());
            jChild.ShouldContainKeyAndValue("Parent", "root");
            var jGrandChild = ((JObject)jChild["GrandChild"]);
            jGrandChild.ShouldContainKeyAndValue("Root", "root");
            jGrandChild.ShouldContainKeyAndValue("Parent", "parent");
            
            var copy = jObject.ToObject<Parent>(CreateSerializer());
            copy.Child.Parent.ShouldBe(copy);
            copy.Child.GrandChild.Root.ShouldBe(copy);
            copy.Child.GrandChild.Parent.ShouldBe(copy.Child);
        }

        [Fact]
        public void ParentReferencesSkipsParentLists()
        {
            serializer.EnableAutomaticBackReferences();

            var root = new Parent();
            var child = new Child
            {
                Parent = root
            };

            root.Children = new List<Child>
            {
                child
            };

            child.GrandChildren = new List<Child>
            {
                new Child
                {
                    Root = root,
                    Parent = child
                }
            };

            var jObject = JObject.FromObject(root, CreateSerializer());

            var jChild = (JObject)((JArray)jObject["Children"])[0];
            jChild.ShouldContainKeyAndValue("Root", JValue.CreateNull());
            jChild.ShouldContainKeyAndValue("Parent", "root");

            var jGrandChild = (JObject)((JArray)jChild["GrandChildren"])[0];
            jGrandChild.ShouldContainKeyAndValue("Root", "root");
            jGrandChild.ShouldContainKeyAndValue("Parent", "parent");

            var copy = jObject.ToObject<Parent>(CreateSerializer());
            copy.Children[0].Parent.ShouldBe(copy);
            copy.Children[0].GrandChildren[0].Root.ShouldBe(copy);
            copy.Children[0].GrandChildren[0].Parent.ShouldBe(copy.Children[0]);
        }

        [Fact]
        public void ThrowsOnDuplicateReferencesNotParentsOrRoot()
        {
            serializer.EnableAutomaticBackReferences();

            var duplicateref = new Derived1();
            var original = new Base
            {
                BaseChild = duplicateref,
                BaseChildren = new List<Base> { duplicateref }
            };

            Should.Throw<InvalidOperationException>(() => JObject.FromObject(original, CreateSerializer()));
        }

        [Fact]
        public void DoesNotThrowOnDuplicatesOfSpecifiedTypes()
        {
            // this will deserialize them as different instances though

            serializer.EnableAutomaticBackReferences(typeof(Derived1));

            var duplicateref = new Derived1();
            var original = new Base
            {
                BaseChild = duplicateref,
                BaseChildren = new List<Base> { duplicateref }
            };

            var jObject = JObject.FromObject(original, CreateSerializer());

            var copy = jObject.ToObject<Base>(CreateSerializer());
            copy.BaseChild.ShouldNotBe(copy.BaseChildren[0]);
        }

        [Fact]
        public void RoundtripsPolymorphicTypesAsDefault()
        {
            var jObject = JObject.FromObject(new Base
            {
                BaseChild = new Derived1(),
                BaseChildren = new List<Base>
                {
                    new Base(),
                    new Derived2(),
                }
            }, CreateSerializer());

            ((JObject)jObject["BaseChild"]).ShouldContainKeyAndValue("$type", "HybridDb.Tests.DefaultSerializerTests+Derived1, HybridDb.Tests");

            var jListWrapper = (JObject) jObject["BaseChildren"];
            var jList = (JArray)jListWrapper["$values"];
            ((JObject)jList[0]).ShouldNotContainKey("$type");
            ((JObject)jList[1]).ShouldContainKeyAndValue("$type", "HybridDb.Tests.DefaultSerializerTests+Derived2, HybridDb.Tests");

            var copy = jObject.ToObject<Base>(CreateSerializer());
            copy.BaseChild.ShouldBeOfType<Derived1>();
            copy.BaseChildren.ShouldBeOfType<List<Base>>();
            copy.BaseChildren[0].ShouldBeOfType<Base>();
            copy.BaseChildren[1].ShouldBeOfType<Derived2>();
        }

        [Fact]
        public void RoundtripsPolymorphicTypesUsingDiscriminators()
        {
            serializer.EnableDiscriminators(
                new Discriminator<Base>("B"),
                new Discriminator<Derived1>("D1"),
                new Discriminator<Derived2>("D2"));

            var jObject = JObject.FromObject(new Base
            {
                BaseChild = new Derived1(),
                BaseChildren = new List<Base>
                {
                    new Base(),
                    new Derived2(),
                }
            }, CreateSerializer());

            ((JObject)jObject["BaseChild"]).ShouldContainKeyAndValue("Discriminator", "D1");

            var jListWrapper = (JObject)jObject["BaseChildren"];
            var jList = (JArray)jListWrapper["$values"]; 
            ((JObject)jList[0]).ShouldContainKeyAndValue("Discriminator", "B");
            ((JObject)jList[1]).ShouldContainKeyAndValue("Discriminator", "D2");

            var copy = jObject.ToObject<Base>(CreateSerializer());
            copy.BaseChild.ShouldBeOfType<Derived1>();
            copy.BaseChildren.ShouldBeOfType<List<Base>>();
            copy.BaseChildren[0].ShouldBeOfType<Base>();
            copy.BaseChildren[1].ShouldBeOfType<Derived2>();
        }

        [Fact]
        public void RootIsNotDiscriminated()
        {

//            jObject.ShouldNotContainKey("Discriminator");
        }

        [Fact]
        public void RoundtripsTypesInObjectArrays()
        {
            serializer.EnableDiscriminators(
                new Discriminator<Base>("B"),
                new Discriminator<Derived1>("D1"),
                new Discriminator<Derived2>("D2"));

            var jObject = JObject.FromObject(new Base
            {
                ObjectChildren = new List<object>
                {
                    new Base(),
                    new Derived2(),
                }
            }, CreateSerializer());

            var jListWrapper = (JObject)jObject["ObjectChildren"];
            var jList = (JArray)jListWrapper["$values"];
            ((JObject)jList[0]).ShouldContainKeyAndValue("Discriminator", "B");
            ((JObject)jList[1]).ShouldContainKeyAndValue("Discriminator", "D2");

            var copy = jObject.ToObject<Base>(CreateSerializer());
            copy.ObjectChildren.ShouldBeOfType<List<object>>();
            copy.ObjectChildren[0].ShouldBeOfType<Base>();
            copy.ObjectChildren[1].ShouldBeOfType<Derived2>();
        }

        [Fact]
        public void UndiscriminatedTypesHaveNoDiscriminator()
        {
            serializer.EnableDiscriminators();

            var jObject = JObject.FromObject(new Root
            {
                Base = new Base()
            }, CreateSerializer());

            var jBase = (JObject)jObject["Base"];
            jBase.ShouldNotContainKey("Discriminiator");

            var copy = jObject.ToObject<Root>(CreateSerializer());
            copy.Base.ShouldBeOfType<Base>();
        }

        [Fact(Skip="Don't know how to implement in a performant manner.")]
        public void ThrowsIfTypeOughtToHaveADiscriminator()
        {
            serializer.EnableDiscriminators();

            Should.Throw<InvalidOperationException>(() =>
                JObject.FromObject(new Root
                {
                    Base = new Derived2()
                }, CreateSerializer()));
        }

        [Fact]
        public void DoesNotOverrideOtherConverters()
        {
            serializer.EnableDiscriminators(
                new Discriminator<Base>("B"),
                new Discriminator<Derived1>("D1"),
                new Discriminator<Derived2>("D2"));

            // Comes after enablind discriptors. Order matters.
            serializer.AddConverter(new StrangeConverterThatAlwaysCreatedAnInstanceOfDerived2());

            var jObject = JObject.FromObject(new Root
            {
                Base = new Derived1()
            }, CreateSerializer());

            jObject["Base"].ShouldBe("**ASGER**");

            var copy = jObject.ToObject<Root>(CreateSerializer());
            copy.Base.ShouldBeOfType<Derived2>();
        }

        public class StrangeConverterThatAlwaysCreatedAnInstanceOfDerived2 : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                writer.WriteValue("**ASGER**");
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                return new Derived2();
            }

            public override bool CanConvert(Type objectType)
            {
                return typeof (Base).IsAssignableFrom(objectType);
            }
        }

        public class Root
        {
            public Base Base { get; set; }
        }

        public class Base
        {
            public Base BaseChild { get; set; }
            public IList<Base> BaseChildren { get; set; }
            public IList<object> ObjectChildren { get; set; }
        }

        class Derived1 : Base {}
        class Derived2 : Base {}

        public class Parent
        {
            public Child Child { get; set; }
            public List<Child> Children { get; set; }
        }

        public class Child
        {
            public object Root { get; set; }
            public object Parent { get; set; }
            public Child GrandChild { get; set; }
            public List<Child> GrandChildren { get; set; }
        }

        public class WithSomePrivates
        {
            public WithSomePrivates()
            {
                PrivateSetProperty = 42;
            }

            string privateField = "lars";
            public string field = "asger";
            public int PrivateSetProperty { get; private set; }
        }

        public class WithThrowingProperty
        {
            public int PropertyWithBackingField
            {
                get { throw new Exception(); }
                set { throw new Exception(); }
            }
        }

        public class WithPropertyWithBackingField
        {
            public int propertyWithBackingField;

            public int PropertyWithBackingField
            {
                get { return propertyWithBackingField * 2; }
                private set { throw new Exception(); }
            }
        }

        public class WithMutatingCtor
        {
            public WithMutatingCtor()
            {
                Value = "FROM CTOR";
            }

            public string Value { get; set; }
        }

        public class WithEvent
        {
            public event EventHandler eventhandler;
            public Action action;
            public Func<int> func;

            public EventHandler GetEventHandler()
            {
                return eventhandler;
            }
        }

        public class WithChaos
        {
            public string String { get; set; }
            public Guid Id { get; set; }
            public List<object> List { get; set; }
            public int Number { get; set; }
        }

        public class StringToStringLengthConverter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                writer.WriteValue(((string)value).Length);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                throw new NotSupportedException();
            }

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(string);
            }
        }
    }
}