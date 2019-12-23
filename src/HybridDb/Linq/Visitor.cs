using System;
using System.Linq.Expressions;

namespace HybridDb.Linq
{
    public class Visitor : ExpressionVisitor
    {
        readonly Func<Expression, Expression> visit;

        public Visitor(Func<Expression, Expression> visit) => this.visit = visit;

        public static Func<Expression> Continue(Func<Expression, Expression> func, Expression exp) =>
            () => new Visitor(func).Continue(exp);

        // Should be called after node has been handled to visit its sub-expressions
        public Expression Continue(Expression node) => base.Visit(node);

        // Called by the visitor for each next sub-expression.
        // The visit-func is responsible for calling Continue to visit further down the tree.
        public override Expression Visit(Expression node) => visit(node);
    }
}