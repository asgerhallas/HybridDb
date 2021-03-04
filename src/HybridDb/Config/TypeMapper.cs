using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Indentional;

namespace HybridDb.Config
{
    public abstract class TypeMapper : ITypeMapper
    {
        readonly IReadOnlyList<Assembly> assemblies;

        protected TypeMapper(IReadOnlyList<Assembly> assemblies = null) => 
            this.assemblies = assemblies ?? AppDomain.CurrentDomain.GetAssemblies();

        public abstract string ToDiscriminator(Type type);

        public Type ToType(string discriminator)
        {
            var types = assemblies.SelectMany(x => x.GetTypes())
                .Where(x => ToDiscriminator(x) == discriminator)
                .ToList();

            var type = types.Count switch
            {
                > 1 => throw new InvalidOperationException(Indent._($@"
                        Too many types found for discriminator '{discriminator}'. Found: 

                            {string.Join(Environment.NewLine, types.Select(x => x.FullName))}")),
                < 1 => throw new InvalidOperationException($@"No type found for discriminator '{discriminator}'."),
                _ => types[0]
            };

            return type;
        }
    }
}