using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

#if NEWTONSOFT

using HybridDb.Serialization;

namespace HybridDb.NewtonsoftJson
{
    public class DefaultSerializer : ISerializer, IDefaultSerializerConfigurator

#else

namespace HybridDb.Serialization
{
    internal class DefaultSerializer : ISerializer, IDefaultSerializerConfigurator

#endif
    {
        Action<JsonSerializerSettings> setup = x => { };

        List<JsonConverter> converters = new List<JsonConverter>();

        readonly List<Func<JsonProperty, bool>> ordering = new List<Func<JsonProperty, bool>>
        {
            p => p.PropertyName == "Id",
            p => p.PropertyType.IsValueType,
            p => p.PropertyType == typeof (string),
            p => !typeof (IEnumerable).IsAssignableFrom(p.PropertyType)
        };

        public DefaultSerializer()
        {
            AddConverters(new StringEnumConverter());
            SetContractResolver(new CachingContractResolverDecorator(new DefaultContractResolver(this)));
        }

        public IExtendedContractResolver ContractResolver { get; private set; }

        public IDefaultSerializerConfigurator EnableAutomaticBackReferences(params Type[] valueTypes)
        {
            SetContractResolver(new AutomaticBackReferencesContractResolverDecorator(ContractResolver));

            Setup(settings =>
            {
                settings.PreserveReferencesHandling = PreserveReferencesHandling.None;
                settings.Context = new StreamingContext(StreamingContextStates.All, new SerializationContext(valueTypes));
            });

            return this;
        }

        public IDefaultSerializerConfigurator EnableDiscriminators(params Discriminator[] discriminators)
        {
            var collection = new Discriminators(discriminators);

            SetContractResolver(new DiscriminatorContractResolverDecorator(ContractResolver, collection));

            AddConverters(new DiscriminatedTypeConverter(collection));
            Order(1, property => property.PropertyName == "Discriminator");
            Setup(settings => { settings.TypeNameHandling = TypeNameHandling.None; });

            return this;
        }

        public void AddConverters(params JsonConverter[] converters)
        {
            this.converters = this.converters.Concat(converters).OrderBy(x => x is DiscriminatedTypeConverter).ToList();
        }

        public void Order(int index, Func<JsonProperty, bool> predicate)
        {
            ordering.Insert(index, predicate);
        }

        public void Setup(Action<JsonSerializerSettings> action)
        {
            setup += action;
        }

        public void SetContractResolver(IExtendedContractResolver resolver)
        {
            ContractResolver = resolver;
        }

        /// <summary>
        /// The reference ids of a JsonSerializer used multiple times will continue to increase on each serialization.
        /// That will result in each serialization to be different and we lose the ability to use it for change tracking.
        /// Therefore we need to create a new serializer each and every time we serialize.
        /// </summary>
        public virtual JsonSerializer CreateSerializer()
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = null,
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
                TypeNameHandling = TypeNameHandling.Auto,
                PreserveReferencesHandling = PreserveReferencesHandling.All,
                ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
                Converters = converters
            };

            setup(settings);

            if (settings.ContractResolver != null)
            {
                throw new InvalidOperationException("Please set ContractResolver with the SetContractResolver() method.");
            }

            settings.ContractResolver = ContractResolver;

            return JsonSerializer.Create(settings);
        }


        public virtual byte[] Serialize(object obj)
        {
            using (var outStream = new MemoryStream())
            using (var bsonWriter = new BsonWriter(outStream))
            {
                CreateSerializer().Serialize(bsonWriter, obj);
                return outStream.ToArray();
            }
        }

        public virtual object Deserialize(byte[] data, Type type)
        {
            using (var inStream = new MemoryStream(data))
            using (var bsonReader = new BsonReader(inStream))
            {
                return CreateSerializer().Deserialize(bsonReader, type);
            }
        }

        public class DefaultContractResolver : Newtonsoft.Json.Serialization.DefaultContractResolver
        {
            readonly static Regex matchesBackingFieldForAutoProperty = new Regex(@"\<(?<name>.*?)\>k__BackingField");
            readonly static Regex matchesFieldNameForAnonymousType = new Regex(@"\<(?<name>.*?)\>i__Field");

            readonly DefaultSerializer serializer;

            public DefaultContractResolver(DefaultSerializer serializer) : base(shareCache: false)
            {
                this.serializer = serializer;
            }
            
            protected override JsonObjectContract CreateObjectContract(Type objectType)
            {
                var contract = base.CreateObjectContract(objectType);
                contract.DefaultCreator = () => FormatterServices.GetUninitializedObject(objectType);
                return contract;
            }

            protected override List<MemberInfo> GetSerializableMembers(Type objectType)
            {
                var members = new List<MemberInfo>();

                while (objectType != null)
                {
                    members.AddRange(objectType
                        .GetMembers(
                            BindingFlags.DeclaredOnly | BindingFlags.Instance |
                            BindingFlags.NonPublic | BindingFlags.Public)
                        .OfType<FieldInfo>()
                        .Where(member => !member.FieldType.IsSubclassOf(typeof(MulticastDelegate))));

                    objectType = objectType.BaseType;
                }

                return members;
            }

            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                return base.CreateProperties(type, memberSerialization)
                    .OrderBy(Ordering).ThenBy(x => x.PropertyName)
                    .ToList();
            }

            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                var property = base.CreateProperty(member, memberSerialization);

                property.Writable = member.MemberType == MemberTypes.Field;
                property.Readable = member.MemberType == MemberTypes.Field;

                NormalizeAutoPropertyBackingFieldName(property);
                NormalizeAnonymousTypeFieldName(property);
                UppercaseFirstLetterOfFieldName(property);

                return property;
            }

            int Ordering(JsonProperty property)
            {
                foreach (var order in serializer.ordering.Select((x, i) => new { check = x, index = i }))
                {
                    if (order.check(property))
                        return order.index;
                }

                return int.MaxValue;
            }

            static void NormalizeAutoPropertyBackingFieldName(JsonProperty property)
            {
                var match = matchesBackingFieldForAutoProperty.Match(property.PropertyName);
                property.PropertyName = match.Success ? match.Groups["name"].Value : property.PropertyName;
            }

            static void NormalizeAnonymousTypeFieldName(JsonProperty property)
            {
                var match = matchesFieldNameForAnonymousType.Match(property.PropertyName);
                property.PropertyName = match.Success ? match.Groups["name"].Value : property.PropertyName;
            }

            static void UppercaseFirstLetterOfFieldName(JsonProperty property)
            {
                property.PropertyName =
                    property.PropertyName.First().ToString(CultureInfo.InvariantCulture).ToUpper() +
                    property.PropertyName.Substring(1);
            }
        }

        public class AutomaticBackReferencesContractResolverDecorator : ExtendedContractResolver
        {
            readonly IExtendedContractResolver resolver;

            public AutomaticBackReferencesContractResolverDecorator(IExtendedContractResolver resolver)
            {
                this.resolver = resolver;
            }

            public override bool ResolveContract(Type type, out JsonContract contract)
            {
                if (resolver.ResolveContract(type, out contract))
                    return true;

                Setup(contract as JsonObjectContract);

                return false;
            }

            static void Setup(JsonObjectContract contract)
            {
                if (contract == null)
                    return;

                contract.OnSerializingCallbacks.Add((current, context) =>
                    ((SerializationContext)context.Context).Push(current));

                contract.OnSerializedCallbacks.Add((current, context) =>
                    ((SerializationContext)context.Context).Pop());

                contract.OnDeserializingCallbacks.Add((current, context) =>
                    ((SerializationContext)context.Context).Push(current));

                contract.OnDeserializedCallbacks.Add((current, context) =>
                    ((SerializationContext)context.Context).Pop());

                contract.OnSerializingCallbacks.Add((value, context) =>
                    ((SerializationContext)context.Context).EnsureNoDuplicates(value));

                foreach (var property in contract.Properties)
                {
                    //if (property.PropertyName == "Root")
                    //    property.Ignored = true;

                    // Assign a "once only" converter to handle back references.
                    // This does not handle the DomainObject.AggregateRoot which is ignored below.
                    if (typeof(object).IsAssignableFrom(property.PropertyType))
                    {
                        property.Converter = property.MemberConverter = new BackReferenceConverter();
                    }
                }
            }
        }

        public class BackReferenceConverter : JsonConverter
        {
            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                var context = ((SerializationContext)serializer.Context.Context);

                if (context.HasRoot && (string)reader.Value == "root")
                {
                    return context.Root;
                }

                if (context.HasParent && (string)reader.Value == "parent")
                {
                    return context.Parent;
                }

                return serializer.Deserialize(reader, objectType);
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var context = ((SerializationContext)serializer.Context.Context);

                if (context.HasRoot && ReferenceEquals(value, context.Root))
                {
                    writer.WriteValue("root");
                    return;
                }

                if (context.HasParent && ReferenceEquals(value, context.Parent))
                {
                    writer.WriteValue("parent");
                    return;
                }

                serializer.Serialize(writer, value);
            }

            public override bool CanConvert(Type objectType)
            {
                throw new NotSupportedException(
                    "This converter is only supposed to be used directly on JsonProperty.Converter and JsonProperty.MemberConverter. " +
                    "If it is registered on the serializer to handle all types it will loop infinitely.");
            }
        }

        public class CachingContractResolverDecorator : ExtendedContractResolver
        {
            readonly IContractResolver resolver;
            readonly ConcurrentDictionary<Type, JsonContract> contracts = new ConcurrentDictionary<Type, JsonContract>();

            public CachingContractResolverDecorator(IContractResolver resolver)
            {
                this.resolver = resolver;
            }

            public override bool ResolveContract(Type type, out JsonContract contract)
            {
                var foundInCache = true;
                contract = contracts.GetOrAdd(type, key =>
                {
                    foundInCache = false;
                    return resolver.ResolveContract(type);
                });

                return foundInCache;
            }
        }

        public class DiscriminatedTypeConverter : JsonConverter
        {
            readonly Discriminators discriminators;

            public DiscriminatedTypeConverter(Discriminators discriminators)
            {
                this.discriminators = discriminators;
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                    return null;

                var jObject = JToken.Load(reader);

                if (jObject.Type == JTokenType.Null)
                    return null;

                var discriminator = jObject.Value<string>("Discriminator");

                if (discriminator == null) return jObject;

                Type type;
                if (!discriminators.TryGetFromDiscriminator(discriminator, out type))
                {
                    throw new InvalidOperationException(string.Format("Could not find a type from discriminator {0}", discriminator));
                }

                var contract = serializer.ContractResolver.ResolveContract(type);
                var target = contract.DefaultCreator();

                serializer.Populate(jObject.CreateReader(), target);

                return target;
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) { }

            public override bool CanWrite
            {
                get { return false; }
            }

            public override bool CanConvert(Type objectType)
            {
                return discriminators.IsDiscriminated(objectType);
            }
        }

        public class DiscriminatorContractResolverDecorator : ExtendedContractResolver
        {
            readonly IExtendedContractResolver resolver;
            readonly Discriminators discriminators;

            public DiscriminatorContractResolverDecorator(IExtendedContractResolver resolver, Discriminators discriminators)
            {
                this.resolver = resolver;
                this.discriminators = discriminators;
            }

            public override bool ResolveContract(Type type, out JsonContract contract)
            {
                if (resolver.ResolveContract(type, out contract))
                    return true;

                Setup(contract as JsonObjectContract);

                return false;
            }

            void Setup(JsonObjectContract contract)
            {
                if (contract == null) return;

                if (!discriminators.IsDiscriminated(contract.CreatedType))
                    return;

                contract.Properties.Insert(0, new JsonProperty
                {
                    PropertyName = "Discriminator",
                    PropertyType = typeof(string),
                    ValueProvider = new DiscriminatorValueProvider(discriminators),
                    Writable = false,
                    Readable = true
                });
            }
        }

        public class DiscriminatorValueProvider : IValueProvider
        {
            readonly Discriminators discriminator;

            public DiscriminatorValueProvider(Discriminators discriminator)
            {
                this.discriminator = discriminator;
            }

            public void SetValue(object target, object value)
            {
                throw new NotSupportedException();
            }

            public object GetValue(object target)
            {
                string value;
                if (!discriminator.TryGetFromType(target.GetType(), out value))
                {
                    throw new InvalidOperationException(string.Format("Type {0} is not discriminated.", target.GetType()));
                }

                return value;
            }
        }
    }

    public interface IExtendedContractResolver : IContractResolver
    {
        bool ResolveContract(Type type, out JsonContract contract);
    }

    public abstract class ExtendedContractResolver : IExtendedContractResolver
    {
        public JsonContract ResolveContract(Type type)
        {
            JsonContract contract;
            ResolveContract(type, out contract);
            return contract;
        }

        /// <summary>
        /// Returns a boolean that indicates if the contract has already been initialized,
        /// so decorators can know if they have additional work to do or can just use the contract as is.
        /// </summary>
        public abstract bool ResolveContract(Type type, out JsonContract contract);
    }
}