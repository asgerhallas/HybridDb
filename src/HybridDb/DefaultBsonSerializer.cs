using System;
using System.Collections.Generic;
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

namespace HybridDb
{
    public class DefaultBsonSerializer : ISerializer
    {
        /// <summary>
        /// The reference ids of a JsonSerializer used multiple times will continue to increase on each serialization.
        /// That will result in each serialization to be different and we lose the ability to use it for change tracking.
        /// Therefore we need to create a new serializer each and every time we serialize.
        /// </summary>
        public JsonSerializer CreateSerializer()
        {
            return new JsonSerializer
            {
                ContractResolver = new DefaultContractResolver(false),
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
                TypeNameHandling = TypeNameHandling.Auto,
                PreserveReferencesHandling = PreserveReferencesHandling.All,
                ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
                Converters =
                {
                    new StringEnumConverter()
                }
            };
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
            readonly Regex matchesBackingFieldForAutoProperty;

            public DefaultContractResolver(bool shareCache)
                : base(shareCache)
            {
                DefaultMembersSearchFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                SerializeCompilerGeneratedMembers = true;
                matchesBackingFieldForAutoProperty = new Regex(@"\<(?<name>.*?)\>");
            }

            protected override JsonObjectContract CreateObjectContract(Type objectType)
            {
                var contract = base.CreateObjectContract(objectType);
                contract.DefaultCreator = () => FormatterServices.GetUninitializedObject(objectType);
                return contract;
            }

            protected override List<MemberInfo> GetSerializableMembers(Type objectType)
            {
                var members = base.GetSerializableMembers(objectType);
                return members.Where(Included).ToList();
            }

            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                var property = base.CreateProperty(member, memberSerialization);

                property.Writable = CanSetMemberValue(member);
                property.Readable = CanGetMemberValue(member);

                var match = matchesBackingFieldForAutoProperty.Match(property.PropertyName);
                if (match.Success)
                {
                    property.PropertyName = match.Groups["name"].Value;
                }

                return property;
            }

            static bool Included(MemberInfo member)
            {
                if (member is PropertyInfo
                    && !IsAutoProperty((PropertyInfo)member))
                    return true;

                if (member is FieldInfo
                    && !((FieldInfo)member).FieldType.IsA(typeof(EventHandler<>)))
                    return true;

                return false;
            }

            static bool CanSetMemberValue(MemberInfo member)
            {
                switch (member.MemberType)
                {
                    case MemberTypes.Field:
                        return true;
                    case MemberTypes.Property:
                        return false;
                    default:
                        return false;
                }
            }

            static bool CanGetMemberValue(MemberInfo member)
            {
                switch (member.MemberType)
                {
                    case MemberTypes.Field:
                        return true;
                    case MemberTypes.Property:
                        return false;
                    default:
                        return false;
                }
            }

            static bool IsAutoProperty(PropertyInfo member)
            {
                bool maybe = member.DeclaringType
                    .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                    .Any(f => f.Name == GetBackingFieldName(member.Name));

                return maybe;
            }

            public static string GetBackingFieldName(string propertyName)
            {
                return "<" + propertyName + ">k__BackingField";
            }
        }
    }
}