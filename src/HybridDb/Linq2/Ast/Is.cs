namespace HybridDb.Linq2.Ast
{
    public class Is : Predicate
    {
        public Is(NullNotNull @case, SqlExpression operand)
        {
            Case = @case;
            Operand = operand;
        }

        public NullNotNull Case { get; set; }
        public SqlExpression Operand { get; }
    }
}