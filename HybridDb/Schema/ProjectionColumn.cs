using System;
using System.Linq.Expressions;
using System.Linq;

namespace HybridDb.Schema
{
    public class ProjectionColumn<TEntity, TMember> : IProjectionColumn
    {
        readonly Expression<Func<TEntity, TMember>> member;
        readonly Func<TEntity, TMember> getter;

        public ProjectionColumn(Expression<Func<TEntity, TMember>> member)
        {
            this.member = member;
            getter = member.Compile();

            var expression = member.ToString();
            Name = string.Join("", expression.Split('.').Skip(1));
            Type = typeof(TMember);

            SqlColumn = new SqlColumn(Type);
        }

        public string Name { get; set; }
        public Type Type { get; set; }
        public SqlColumn SqlColumn { get; private set; }

        public object GetValue(object document)
        {
            //TODO: Maybe travser instead, as we are hiding potential important NRE exceptions
            try
            {
                return getter((TEntity)document);
            }
            catch (NullReferenceException)
            {
                return null;
            }
        }
    }
}