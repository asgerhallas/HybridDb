using System.Diagnostics;

namespace HybridDb.Linq.Old.Ast
{
    [DebuggerDisplay("NodeType={NodeType}")]
    public abstract class SqlExpression
    {
        public abstract SqlNodeType NodeType { get; }
    }
}