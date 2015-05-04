using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using HybridDb.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace HybridDb.Serialization
{
    public class DefaultSerializer : ISerializer, IDefaultSerializerConfigurator
    {
        Action<JsonSerializerSettings> setup = x => { };

        IExtendedContractResolver contractResolver;
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
            AddConverter(new StringEnumConverter());
            SetContractResolver(new CachingContractResolverDecorator(new DefaultContractResolver(this)));
        }

        public void AddConverter(JsonConverter converter)
        {
            converters = converters.Concat(new[] { converter }).OrderBy(x => x is DiscriminatedTypeConverter).ToList();
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
            contractResolver = resolver;
        }

        public void EnableAutomaticBackReferences(params Type[] valueTypes)
        {
            SetContractResolver(new AutomaticBackReferencesContractResolverDecorator(contractResolver));

            Setup(settings =>
            {
                settings.PreserveReferencesHandling = PreserveReferencesHandling.None;
                settings.Context = new StreamingContext(StreamingContextStates.All, new SerializationContext(valueTypes));
            });
        }

        public void EnableDiscriminators(params Discriminator[] discriminators)
        {
            var collection = new Discriminators(discriminators);

            SetContractResolver(new DiscriminatorContractResolverDecorator(contractResolver, collection));

            AddConverter(new DiscriminatedTypeConverter(collection, converters));
            Order(1, property => property.PropertyName == "Discriminator");
            Setup(settings => { settings.TypeNameHandling = TypeNameHandling.None; });
        }

        /// <summary>
        /// The reference ids of a JsonSerializer used multiple times will continue to increase on each serialization.
        /// That will result in each serialization to be different and we lose the ability to use it for change tracking.
        /// Therefore we need to create a new serializer each and every time we serialize.
        /// </summary>
        public JsonSerializer CreateSerializer()
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

            settings.ContractResolver = contractResolver;

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
    }
}