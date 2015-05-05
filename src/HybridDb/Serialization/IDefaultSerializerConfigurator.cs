using System;

namespace HybridDb.Serialization
{
    public interface IDefaultSerializerConfigurator
    {
        IDefaultSerializerConfigurator EnableAutomaticBackReferences(params Type[] valueTypes);
        IDefaultSerializerConfigurator EnableDiscriminators(params Discriminator[] discriminators);
    }
}