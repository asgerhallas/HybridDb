using System;
using System.Collections.Generic;

namespace HybridDb.Linq.Ast
{
    public static class OperationParser
    {
        public static SqlExpression ParseToSqlExpression(this Stack<Operation> operations)
        {
            var output = new Stack<SqlExpression>();
            while (operations.Count > 0)
            {
                var instruction = operations.Pop();
                switch (instruction.NodeType)
                {
                    case SqlNodeType.And:
                    case SqlNodeType.Or:
                    case SqlNodeType.Equal:
                        output.Push(new SqlBinaryExpression(instruction.NodeType,
                                                            output.Pop(),
                                                            output.Pop()));
                        break;
                    case SqlNodeType.Column:
                        output.Push(new SqlColumnExpression((string) instruction.Value));
                        break;
                    case SqlNodeType.Constant:
                        output.Push(new SqlConstantExpression(instruction.Value));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (output.Count != 1)
                throw new InvalidOperationException("Unbalanced stack");

            return output.Pop();
        }
    }
}