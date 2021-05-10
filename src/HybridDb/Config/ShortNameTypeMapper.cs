using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Indentional;
using Sprache;

namespace HybridDb.Config
{
    public class ShortNameTypeMapper : ITypeMapper
    {
        readonly IReadOnlyList<Assembly> assemblies;

        public ShortNameTypeMapper(IReadOnlyList<Assembly> assemblies = null) => this.assemblies = assemblies;

        public string ToDiscriminator(Type type)
        {
            var shortname = ShortNameByType(type);

            if (!type.IsGenericType) return shortname;

            if (!type.IsConstructedGenericType)
                throw new NotSupportedException("Cannot discriminate a open generic type.");

            return $"{shortname}({string.Join("|", type.GetGenericArguments().Select(ToDiscriminator))})";
        }

        public Type ToType(Type basetype, string discriminator) => TypeParser(basetype).End().Parse(discriminator);

        public static Parser<string> ShortName = Parse.AnyChar.Except(Parse.Chars('(', '|', ')')).AtLeastOnce().Text();

        public Parser<Type> SimpleType(Type basetype) =>
            from shortname in ShortName
            where !string.IsNullOrEmpty(shortname)
            select TypeByShortName(basetype, shortname);

        public Parser<Type> GenericType(Type basetype) =>
            from shortname in ShortName
            from types in TypeParser(basetype).DelimitedBy(Parse.Char('|')).Contained(Parse.Char('('), Parse.Char(')'))
            select TypeByShortName(basetype, shortname).MakeGenericType(types.ToArray());

        public Parser<Type> TypeParser(Type basetype) => GenericType(basetype).Or(SimpleType(basetype));
        
        static string ShortNameByType(Type type) =>
            type.IsNested
                ? $"{type.DeclaringType.Name}+{type.Name}"
                : type.Name;

        Type TypeByShortName(Type basetype, string shortname)
        {
            var types = (assemblies ?? AppDomain.CurrentDomain.GetAssemblies())
                .SelectMany(x => x.GetTypes())
                .Where(x => ShortNameByType(x) == shortname && basetype.IsAssignableFrom(x))
                .ToList();

            var type = types.Count switch
            {
                > 1 => throw new InvalidOperationException(Indent._($@"
                        Too many types found for '{shortname}'. Found: 

                            {string.Join(Environment.NewLine, types.Select(x => x.FullName))}")),
                < 1 => throw new InvalidOperationException($@"No type found for '{shortname}'."),
                _ => types[0]
            };

            return type;
        }
    }
}