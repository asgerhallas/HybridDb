using System;
using System.Linq.Expressions;

namespace HybridDb.Config
{
    public class IndexDesigner<TIndex, TEntity>
    {
        readonly Func<Expression, string> columnNameConvention;
        readonly DocumentDesigner<TEntity> designer;

        public IndexDesigner(DocumentDesign design, Func<Expression, string> columnNameConvention)
        {
            this.columnNameConvention = columnNameConvention;
            designer = new DocumentDesigner<TEntity>(design, columnNameConvention);
        }

        public IndexDesigner<TIndex, TEntity> With<TMember>(Expression<Func<TIndex, TMember>> namer, Expression<Func<TEntity, TMember>> projector, params Option[] options)
        {
            var name = columnNameConvention(namer);
            designer.With(name, projector, options);
            return this;
        }
    }
}