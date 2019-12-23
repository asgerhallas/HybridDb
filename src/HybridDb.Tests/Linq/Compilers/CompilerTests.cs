using System;
using System.Linq.Expressions;
using HybridDb.Linq.Compilers;

namespace HybridDb.Tests.Linq.Compilers
{
    public class CompilerTests
    {
        protected Expression F<T>(Expression<Func<T, object>> exp) => exp;
        protected Expression F<T>(Expression<Func<View<T>, T, object>> exp) => exp;
        protected View<T> View<T>(string key, T t) => new View<T>(key, t);
    }
}