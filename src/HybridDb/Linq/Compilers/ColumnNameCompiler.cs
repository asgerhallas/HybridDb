using System.Linq.Expressions;
using HybridDb.Linq.Bonsai;
using ShinySwitch;

namespace HybridDb.Linq.Compilers
{
    public class ColumnNameCompiler : LinqPlugin
    {
        public override BonsaiExpression Compile(Expression exp, Compiler top, Compiler next) => Switch<BonsaiExpression>.On(exp)
            .Match<ParameterExpression>(parameter => new Column(null, typeof(View).IsAssignableFrom(parameter.Type), parameter.Type))  // TODO: lookinto IsMetadata
            .Match<MemberExpression>(member =>
            {
                var receiver = (Column) Compile(member.Expression, top, next);

                var receiverString = receiver.Name != null ? $"{receiver.Name}" : "";

                return new Column($"{receiverString}{member.Member.Name}", receiver.IsMetadata, member.Member.Type());
            })
            .Else(() => next(exp));
    }
    public class View
    {
        public View(string key, object data)
        {
            Key = key;
            Data = data;
        }

        public string Key { get; }
        public object Data { get; }
        public long Position { get; set; }
    }

    public sealed class View<T> : View
    {
        public View(string key, T data)
            : base(key, data) => Data = data;

        public new T Data { get; }
    }

}