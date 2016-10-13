using System;

namespace HybridDb.Linq2.Emitter
{
    public static class EmitHelper
    {
        public static Func<T2, T3> Apply<T1, T2, T3>(this T1 arg1, Func<T1, T2, T3> func)
        {
            return arg2 => func(arg1, arg2);
        }

        public static T3 Apply<T1, T2, T3>(this T1 arg1, Func<T1, T2, T3> func, T2 arg2)
        {
            return func(arg1, arg2);
        }

        public static T4 Apply<T1, T2, T3, T4>(this T1 arg1, Func<T1, T2, T3, T4> func, T2 arg2, T3 arg3)
        {
            return func(arg1, arg2, arg3);
        }
    }
}