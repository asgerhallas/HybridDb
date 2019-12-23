using System.Linq.Expressions;

namespace HybridDb.Linq
{
    public class LinqCompiler
    {
        public LinqCompiler(Compiler frontend, Emitter backend)
        {
            Frontend = frontend;
            Backend = backend;
        }

        public Compiler Frontend { get; }
        public Emitter Backend { get; }

        public string Compile(Expression expression) => Backend(Frontend(expression));
    }
}