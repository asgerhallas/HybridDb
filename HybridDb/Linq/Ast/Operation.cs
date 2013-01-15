using System.Diagnostics;

namespace HybridDb.Linq.Ast
{
    [DebuggerDisplay("NodeType={NodeType}, Value={Value}")]
    public struct Operation
    {
        readonly SqlNodeType nodeType;

        readonly object value;

        public Operation(SqlNodeType nodeType) : this(nodeType, null) {}

        public Operation(SqlNodeType nodeType, object value)
        {
            this.nodeType = nodeType;
            this.value = value;
        }

        public SqlNodeType NodeType
        {
            get { return nodeType; }
        }

        public object Value
        {
            get { return value; }
        }
    }
}