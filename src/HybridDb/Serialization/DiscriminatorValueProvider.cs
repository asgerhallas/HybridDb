using System;
using Newtonsoft.Json.Serialization;

namespace HybridDb.Serialization
{
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