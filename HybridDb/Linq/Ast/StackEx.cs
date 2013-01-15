using System.Collections.Generic;

namespace HybridDb.Linq.Ast
{
    public static class StackEx
    {
        public static IEnumerable<T> Pop<T>(this Stack<T> stack, int number)
        {
            for (var i = 0; i < number; i++)
                yield return stack.Pop();
        }
    }
}