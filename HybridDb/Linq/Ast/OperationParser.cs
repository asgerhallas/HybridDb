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
                    case SqlNodeType.Project:
                        output.Push(new SqlProjectionExpression(
                                        ((SqlColumnExpression) output.Pop()).ColumnName,
                                        ((SqlColumnExpression) output.Pop()).ColumnName));
                        break;
                    case SqlNodeType.Where:
                        output.Push(new SqlWhereExpression((SqlBinaryExpression) output.Pop()));
                        break;
                    case SqlNodeType.Not:
                        output.Push(new SqlNotExpression(output.Pop()));
                        break;
                    case SqlNodeType.And:
                    case SqlNodeType.Or:
                    case SqlNodeType.LessThan:
                    case SqlNodeType.LessThanOrEqual:
                    case SqlNodeType.GreaterThan:
                    case SqlNodeType.GreaterThanOrEqual:
                    case SqlNodeType.BitwiseAnd:
                    case SqlNodeType.BitwiseOr:
                        output.Push(new SqlBinaryExpression(instruction.NodeType, output.Pop(), output.Pop()));
                        break;
                    case SqlNodeType.Equal:
                    case SqlNodeType.NotEqual:
                        var left = output.Pop();
                        var right = output.Pop();

                        if (right.NodeType == SqlNodeType.Constant && ((SqlConstantExpression)right).Value == null)
                        {
                            output.Push(new SqlBinaryExpression(instruction.NodeType == SqlNodeType.Equal
                                                                    ? SqlNodeType.Is
                                                                    : SqlNodeType.IsNot, left, right));
                        }
                        else
                        {
                            output.Push(new SqlBinaryExpression(instruction.NodeType, left, right));
                        }

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