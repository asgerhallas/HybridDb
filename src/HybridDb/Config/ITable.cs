using System;
using System.Collections.Generic;

namespace HybridDb.Config
{
    public interface IProjectionTable
    {
        string Name { get; }
        IEnumerable<Column> Columns { get; }
        SystemColumn DocumentIdColumn { get; }
        Column this[string name] { get; }
        Column GetColumnOrDefaultDynamicColumn(string name, Type type);
        void AddProjection(Column column);
    }
    
    //public interface ITable
    //{
    //    string Name { get; }
    //    IEnumerable<Column> Columns { get; }
    //    IdColumn IdColumn { get; }
    //    EtagColumn EtagColumn { get; }
    //    DocumentColumn DocumentColumn { get; }
    //    Column this[string name] { get; }
    //    Column GetColumnOrDefaultDynamicColumn(string name, Type type);
    //    void AddProjection(UserColumn column);
    //}
}