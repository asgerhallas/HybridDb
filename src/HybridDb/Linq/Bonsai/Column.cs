using System;

namespace HybridDb.Linq.Bonsai
{
    public class Column : BonsaiExpression
    {
        public Column(string name, bool isMetadata, Type type) : base(type)
        {
            Name = name;
            IsMetadata = isMetadata;
        }

        public string Name { get; }
        public bool IsMetadata { get; }
    }
}