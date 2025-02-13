#nullable enable
using Microsoft.Data.SqlClient;

namespace HybridDb.SqlBuilder
{
    public abstract record Fragment;
    public record StringFragment(string Value) : Fragment;
    public record ParameterFragment(SqlParameter Parameter) : Fragment;
    public record TableFragment(string TableName) : Fragment;
    public record ColumnFragment(string ColumnName) : Fragment;
}