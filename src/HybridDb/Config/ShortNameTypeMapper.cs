using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sprache;
using static Indentional.Text;

namespace HybridDb.Config
{
    public class ShortNameTypeMapper : ITypeMapper
    {
        readonly HashSet<Assembly> assemblies = new();
        readonly Dictionary<string, List<Type>> typesByShortName = new();

        public ShortNameTypeMapper(params Assembly[] assemblies)
        {
            // Add the the system types from CoreLib
            Add(typeof(int).Assembly);

            foreach (var assembly in assemblies)
            {
                Add(assembly);
            }
        }

        public void Add(Assembly assembly)
        {
            if (!assemblies.Add(assembly)) return;

            foreach (var type in assembly.GetTypes())
            {
                var shortname = ShortNameByType(type);

                if (!typesByShortName.TryGetValue(shortname, out var list))
                {
                    list = typesByShortName[shortname] = new List<Type>();
                }

                list.Add(type);
            }
        }

        public string ToDiscriminator(Type type)
        {
            if (!assemblies.Contains(type.Assembly))
            {
                throw new InvalidOperationException(
                    Indent($"""
                     Type '{type.FullName}' cannot get a shortname discriminator as the assembly is not known to HybridDb.
                     Only assemblies of types that are configured with configuration.Document<T>(), CoreLib and 
                     the assemblies in which the DocumentStore are instantiated are known by default.
                     Please add a call to `configuration.UseTypeMapper(new ShortNameTypeMapper(typeof({type.Name}).Assembly));`
                     or 'configuration.TypeMapper.Add(typeof({type.Name}).Assembly);' to your HybridDb configuration.
                     """));
            }

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
            if (!typesByShortName.TryGetValue(shortname, out var matchesByShortName))
            {
                matchesByShortName = new List<Type>();
            }

            var matchesByBaseType = matchesByShortName.Where(basetype.IsAssignableFrom).ToList();

            var type = matchesByBaseType.Count switch
            {
                > 1 => throw new InvalidOperationException(
                    Indent(
                        $"""
                         Too many types found for '{shortname}'. Found:
                        
                         {string.Join(Environment.NewLine, matchesByBaseType.Select(x => x.FullName))}
                         """)),
                < 1 => throw new InvalidOperationException($@"No type found for '{shortname}'."),
                _ => matchesByBaseType[0]
            };
            return type;
        }
    }
}