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
using Newtonsoft.Json.Serialization;

namespace HybridDb.Serialization
{
    //public class Discriminators
    //{
    //    readonly IDiscriminator[] discriminators;

    //    public Discriminators()
    //    {
    //        discriminators = new IDiscriminator[]
    //        {
    //            new RootDiscriminator(),
    //            new FieldDataDiscriminator(),
    //            new CaseProgressDiscriminator(),
    //            new PurchaseStateDiscriminator(),
    //            new TemplateDiscriminator(),
    //            new BuildingUnitDiscriminator(),
    //            new ContextDiscriminator(),
    //            new EnergyLabelClassificationScaleDiscriminator(),
    //            new UnitPricedFuelExchangeDiscriminator(),
    //        };

    //        Context = new StreamingContext(
    //                StreamingContextStates.All,
    //                new SerializationContext(
    //                    typeof(Scenario),
    //                    typeof(EnergyRequirement),
    //                    typeof(EnergyFrameCompliance),
    //                    typeof(EnergyFrame),
    //                    typeof(FullResult),
    //                    typeof(ResultYear),
    //                    typeof(BeResult),
    //                    typeof(Fuel))),

    //            new DiscriminatedTypeConverter(discriminators),

    //    }


    //    public IDiscriminator GetDiscriminatorFor(Type type)
    //    {
    //        return discriminators.SingleOrDefault(x => x.Discriminates(type));
    //    }
    //EnableCheckForDuplicates(contract);
    //EnableAutomaticBackReferences(contract);
    //EnableInvisibleAggregateRootReferences(contract);
    //EnableInvisibleDomainEventsCollection(contract);


                //var discriminator = serializer.GetDiscriminatorFor(type);
                //if (discriminator != null)
                //{
                //    properties.Add(new JsonProperty
                //    {
                //        PropertyName = "Discriminator",
                //        PropertyType = typeof(string),
                //        ValueProvider = new DiscriminatorValueProvider(discriminator),
                //        Writable = false,
                //        Readable = true
                //    });
                //}

    //}


    public class DefaultSerializer : ISerializer
    {
        readonly JsonConverter[] converters;
        readonly DefaultContractResolver contractResolver;

        public DefaultSerializer()
        {
            converters = new JsonConverter[]
            {
                new StringEnumConverter(), 
                //new GuidToStringConverter(),
            };

            contractResolver = new DefaultContractResolver();
        }

        /// <summary>
        /// The reference ids of a JsonSerializer used multiple times will continue to increase on each serialization.
        /// That will result in each serialization to be different and we lose the ability to use it for change tracking.
        /// Therefore we need to create a new serializer each and every time we serialize.
        /// </summary>
        public JsonSerializer CreateSerializer()
        {
            return JsonSerializer.Create(new JsonSerializerSettings
            {
                ContractResolver = contractResolver,
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
                TypeNameHandling = TypeNameHandling.Auto,
                PreserveReferencesHandling = PreserveReferencesHandling.All,
                ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
                
                Converters = converters
            });
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

            readonly ConcurrentDictionary<Type, JsonContract> contracts = new ConcurrentDictionary<Type, JsonContract>();

            public DefaultContractResolver() : base(shareCache: false) { }

            public override JsonContract ResolveContract(Type type)
            {
                return contracts.GetOrAdd(type, key => base.ResolveContract(type));
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
                        .Where(member => !member.FieldType.IsA(typeof (EventHandler<>))));

                    objectType = objectType.BaseType;
                }

                return members;
            }

            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                var properties = base.CreateProperties(type, memberSerialization);
                return properties.OrderBy(Ordering).ThenBy(x => x.PropertyName).ToList();
            }

            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                var property = base.CreateProperty(member, memberSerialization);

                property.Writable = member.MemberType == MemberTypes.Field;
                property.Readable = member.MemberType == MemberTypes.Field;

                NormalizeAutoPropertyBackingFieldName(property);
                UppercaseFirstLetterOfFieldName(property);

                return property;
            }

            static int Ordering(JsonProperty property)
            {
                var orders = new List<Func<JsonProperty, bool>>
                {
                    p => p.PropertyName == "Id",
                    p => property.PropertyType.IsValueType,
                    p => property.PropertyType == typeof(string),
                    p => !typeof(IEnumerable).IsAssignableFrom(property.PropertyType)
                };

                foreach (var order in orders.Select((x, i) => new { check = x, index = i }))
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

            static void UppercaseFirstLetterOfFieldName(JsonProperty property)
            {
                property.PropertyName =
                    property.PropertyName.First().ToString(CultureInfo.InvariantCulture).ToUpper() +
                    property.PropertyName.Substring(1);
            }
        }
    }
}