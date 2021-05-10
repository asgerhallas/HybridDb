﻿using System;

namespace HybridDb.Config
{
    public interface ITypeMapper
    {
        string ToDiscriminator(Type type);
        Type ToType(Type basetype, string discriminator);
    }
}