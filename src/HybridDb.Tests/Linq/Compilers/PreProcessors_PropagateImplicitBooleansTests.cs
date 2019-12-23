using System;
using System.Linq.Expressions;
using HybridDb.Linq.Compilers;
using ShouldBeLike;
using Xunit;

namespace HybridDb.Tests.Linq.Compilers
{
    public class PreProcessors_PropagateImplicitBooleansTests
    {
        [Fact]
        public void Property()
        {
            var parameter = Expression.Parameter(typeof(A), "x");

            PreProcessors.PropagateImplicitBooleans(ExpOf<A>(x => x.BoolProperty))
                .ShouldBeLike(Expression.Lambda<Func<A, bool>>(
                    Expression.MakeBinary(ExpressionType.Equal,
                        Expression.MakeMemberAccess(parameter, typeof(A).GetMember(nameof(A.BoolProperty))[0]),
                        Expression.Constant(true)),
                    parameter));
        }

        [Fact]
        public void Field()
        {
            var parameter = Expression.Parameter(typeof(A), "x");

            PreProcessors.PropagateImplicitBooleans(ExpOf<A>(x => x.BoolField))
                .ShouldBeLike(Expression.Lambda<Func<A, bool>>(
                    Expression.MakeBinary(ExpressionType.Equal,
                        Expression.MakeMemberAccess(parameter, typeof(A).GetMember(nameof(A.BoolField))[0]),
                        Expression.Constant(true)),
                    parameter));
        }

        [Fact]
        public void SkipExplicitBoolComparisons()
        {
            var parameter = Expression.Parameter(typeof(A), "x");

            // ReSharper disable once RedundantBoolCompare
            PreProcessors.PropagateImplicitBooleans(ExpOf<A>(x => x.BoolProperty == true))
                .ShouldBeLike(Expression.Lambda<Func<A, bool>>(
                    Expression.MakeBinary(ExpressionType.Equal,
                        Expression.MakeMemberAccess(parameter, typeof(A).GetMember(nameof(A.BoolProperty))[0]),
                        Expression.Constant(true)),
                    parameter));
        }

        [Fact]
        public void SkipOtherComparisons()
        {
            var parameter = Expression.Parameter(typeof(A), "x");

            PreProcessors.PropagateImplicitBooleans(ExpOf<A>(x => x.BoolProperty == x.BoolField))
                .ShouldBeLike(Expression.Lambda<Func<A, bool>>(
                    Expression.MakeBinary(ExpressionType.Equal,
                        Expression.MakeMemberAccess(parameter, typeof(A).GetMember(nameof(A.BoolProperty))[0]),
                        Expression.MakeMemberAccess(parameter, typeof(A).GetMember(nameof(A.BoolField))[0])),
                    parameter));
        }

        [Fact]
        public void AndAlso()
        {
            var parameter = Expression.Parameter(typeof(A), "x");

            PreProcessors.PropagateImplicitBooleans(ExpOf<A>(x => x.BoolProperty && x.BoolField))
                .ShouldBeLike(Expression.Lambda<Func<A, bool>>(
                    Expression.MakeBinary(ExpressionType.AndAlso,
                        Expression.MakeBinary(ExpressionType.Equal,
                            Expression.MakeMemberAccess(parameter, typeof(A).GetMember(nameof(A.BoolProperty))[0]),
                            Expression.Constant(true)),
                        Expression.MakeBinary(ExpressionType.Equal,
                            Expression.MakeMemberAccess(parameter, typeof(A).GetMember(nameof(A.BoolField))[0]),
                            Expression.Constant(true))),
                    parameter));
        }

        static Expression ExpOf<T>(Expression<Func<T, bool>> exp) => exp;

        public class A
        {
            public MyEnum EnumProperty { get; set; }
            public bool BoolProperty { get; set; }
            public bool BoolField;
        }

        public enum MyEnum
        {
            X,
            Y,
            Z
        }

    }
}