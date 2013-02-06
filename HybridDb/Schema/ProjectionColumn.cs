using System;
using System.Linq.Expressions;
using System.Linq;

namespace HybridDb.Schema
{
    public abstract class ProjectionColumn : Column
    {
        public abstract object GetValue(object document);
    }

    public class ProjectionColumn<TEntity, TMember> : ProjectionColumn
    {
        readonly Func<TEntity, TMember> getter;

        public ProjectionColumn(Expression<Func<TEntity, TMember>> member)
        {
            getter = member.Compile();

            var expression = member.ToString();
            Name = string.Join("", expression.Split('.').Skip(1));

            SqlColumn = new SqlColumn(typeof(TMember));
        }

        public override object GetValue(object document)
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