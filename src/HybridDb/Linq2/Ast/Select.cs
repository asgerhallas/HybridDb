using System.Collections.Generic;
using System.Linq;
using HybridDb.Linq.Ast;

namespace HybridDb.Linq2.Ast
{
    //TODO: Enforce before emit that Select has at least one SelectColumn
    public class Select : SqlClause
    {
        // NOTE: If extending this with expression be aware that parameters are not allowed in select lists
        public Select(IEnumerable<SelectColumn> selectList)
        {
            SelectList = selectList.ToList();
        }

        public Select(params SelectColumn[] selectList) : this((IEnumerable<SelectColumn>)selectList) { }

        public IReadOnlyList<SelectColumn> SelectList { get; }
    }
}