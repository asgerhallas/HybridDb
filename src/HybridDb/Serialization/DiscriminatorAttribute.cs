using System;

namespace HybridDb.Serialization
{
    public class DiscriminatorAttribute : Attribute
    {
        readonly string discriminator;

        public DiscriminatorAttribute(string discriminator)
        {
            this.discriminator = discriminator;
        }

        public string Discriminator
        {
            get { return discriminator; }
        }
    }
}