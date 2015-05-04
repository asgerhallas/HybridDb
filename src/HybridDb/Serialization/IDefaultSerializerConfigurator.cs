using System;

namespace HybridDb.Serialization
{
    public interface IDefaultSerializerConfigurator
    {
        void EnableAutomaticBackReferences(params Type[] valueTypes);
        void EnableDiscriminators(params Discriminator[] discriminators);
    }
}