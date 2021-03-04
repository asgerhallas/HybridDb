using System;
using System.Linq;
using Indentional;
using Sprache;

namespace HybridDb.Config
{
    public class ShortNameTypeMapper : ITypeMapper
    {
        public string ToDiscriminator(Type type)
        {
            var shortname = ShortNameByType(type);

            if (!type.IsGenericType) return shortname;

            if (!type.IsConstructedGenericType)
                throw new NotSupportedException("Cannot discriminate a open generic type.");

            return $"{shortname}({string.Join("|", type.GetGenericArguments().Select(ToDiscriminator))})";
        }

        public Type ToType(string discriminator) => Type.End().Parse(discriminator);

        public static Parser<string> ShortName = Parse.AnyChar.Except(Parse.Chars('(', '|', ')')).AtLeastOnce().Text();

        public static Parser<Type> SimpleType =
            from shortname in ShortName
            where !string.IsNullOrEmpty(shortname)
            select TypeByShortName(shortname);

        public static Parser<Type> GenericType =
            from shortname in ShortName
            from types in Type.DelimitedBy(Parse.Char('|')).Contained(Parse.Char('('), Parse.Char(')'))
            select TypeByShortName(shortname).MakeGenericType(types.ToArray());

        public static Parser<Type> Type = GenericType.Or(SimpleType);
        
        static string ShortNameByType(Type type) =>
            type.IsNested
                ? $"{type.DeclaringType.Name}+{type.Name}"
                : type.Name;

        static Type TypeByShortName(string shortname)
        {
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(x => x.GetTypes())
                .Where(x => ShortNameByType(x) == shortname)
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