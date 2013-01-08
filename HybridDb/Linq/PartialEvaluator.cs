using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace HybridDb.Linq
{
    /// <summary>
    ///     Rewrites an expression tree so that locally isolatable sub-expressions are evaluated and converted into ConstantExpression nodes.
    /// </summary>
    public static class PartialEvaluator
    {
        /// <summary>
        ///     Performs evaluation & replacement of independent sub-trees
        /// </summary>
        /// <param name="expression">The root of the expression tree.</param>
        /// <returns>A new tree with sub-trees evaluated and replaced.</returns>
        public static Expression Eval(Expression expression)
        {
            return Eval(expression, null);
        }

        /// <summary>
        ///     Performs evaluation & replacement of independent sub-trees
        /// </summary>
        /// <param name="expression">The root of the expression tree.</param>
        /// <param name="fnCanBeEvaluated">A function that decides whether a given expression node can be part of the local function.</param>
        /// <returns>A new tree with sub-trees evaluated and replaced.</returns>
        public static Expression Eval(Expression expression, Func<Expression, bool> fnCanBeEvaluated)
        {
            if (fnCanBeEvaluated == null)
                fnCanBeEvaluated = CanBeEvaluatedLocally;
            return SubtreeEvaluator.Eval(Nominator.Nominate(fnCanBeEvaluated, expression), expression);
        }

        static bool CanBeEvaluatedLocally(Expression expression)
        {
            return expression.NodeType != ExpressionType.Parameter 
                && expression.NodeType != ExpressionType.Call;
        }

        /// <summary>
        ///     Performs bottom-up analysis to determine which nodes can possibly
        ///     be part of an evaluated sub-tree.
        /// </summary>
        class Nominator : ExpressionVisitor
        {
            readonly HashSet<Expression> candidates;
            readonly Func<Expression, bool> fnCanBeEvaluated;
            bool cannotBeEvaluated;

            Nominator(Func<Expression, bool> fnCanBeEvaluated)
            {
                candidates = new HashSet<Expression>();
                this.fnCanBeEvaluated = fnCanBeEvaluated;
            }

            internal static HashSet<Expression> Nominate(Func<Expression, bool> fnCanBeEvaluated, Expression expression)
            {
                var nominator = new Nominator(fnCanBeEvaluated);
                nominator.Visit(expression);
                return nominator.candidates;
            }

            protected override Expression VisitConstant(ConstantExpression c)
            {
                return base.VisitConstant(c);
            }

            public override Expression Visit(Expression expression)
            {
                if (expression != null)
                {
                    var saveCannotBeEvaluated = cannotBeEvaluated;
                    cannotBeEvaluated = false;
                    base.Visit(expression);
                    if (!cannotBeEvaluated)
                    {
                        if (fnCanBeEvaluated(expression))
                        {
                            candidates.Add(expression);
                        }
                        else
                        {
                            cannotBeEvaluated = true;
                        }
                    }
                    cannotBeEvaluated |= saveCannotBeEvaluated;
                }
                return expression;
            }
        }

        /// <summary>
        ///     Evaluates & replaces sub-trees when first candidate is reached (top-down)
        /// </summary>
        class SubtreeEvaluator : ExpressionVisitor
        {
            readonly HashSet<Expression> candidates;

            SubtreeEvaluator(HashSet<Expression> candidates)
            {
                this.candidates = candidates;
            }

            internal static Expression Eval(HashSet<Expression> candidates, Expression exp)
            {
                return new SubtreeEvaluator(candidates).Visit(exp);
            }

            public override Expression Visit(Expression exp)
            {
                if (exp == null)
                {
                    return null;
                }

                if (candidates.Contains(exp))
                {
                    return Evaluate(exp);
                }

                return base.Visit(exp);
            }

            Expression Evaluate(Expression e)
            {
                var type = e.Type;

                // check for nullable converts & strip them
                if (e.NodeType == ExpressionType.Convert)
                {
                    var u = (UnaryExpression) e;
                    if (u.Operand.Type.AsNonNullable() == type.AsNonNullable())
                    {
                        e = ((UnaryExpression) e).Operand;
                    }
                }

                // if we now just have a constant, return it
                if (e.NodeType == ExpressionType.Constant)
                {
                    var ce = (ConstantExpression) e;

                    // if we've lost our nullable typeness add it back
                    if (e.Type != type && e.Type.AsNonNullable() == type.AsNonNullable())
                    {
                        e = Expression.Constant(ce.Value, type);
                    }

                    return e;
                }

                var me = e as MemberExpression;
                if (me != null)
                {
                    // member accesses off of constant's are common, and yet since these partial evals
                    // are never re-used, using reflection to access the member is faster than compiling  
                    // and invoking a lambda
                    var ce = me.Expression as ConstantExpression;
                    if (ce != null)
                    {
                        return Expression.Constant(me.Member.GetValue(ce.Value), type);
                    }
                }

                if (type.IsValueType)
                {
                    e = Expression.Convert(e, typeof (object));
                }

                var lambda = Expression.Lambda<Func<object>>(e);
                var fn = lambda.Compile();
                return Expression.Constant(fn(), type);
            }
        }
    }
}