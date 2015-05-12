using System;
using System.Linq.Expressions;

namespace HybridDb.Serialization
{
    public interface IDefaultSerializerConfigurator
    {
        IDefaultSerializerConfigurator EnableAutomaticBackReferences(params Type[] valueTypes);
        IDefaultSerializerConfigurator EnableDiscriminators(params Discriminator[] discriminators);
        IDefaultSerializerConfigurator Hide<T, TReturn>(Expression<Func<T, TReturn>> selector, Func<TReturn> @default);
        IDefaultSerializerConfigurator Hide<T>(string selector, Func<object> @default);
    }
}