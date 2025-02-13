#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using HybridDb.Config;

namespace HybridDb.SqlBuilder
{
    [InterpolatedStringHandler]
    public readonly struct SqlStringHandler
    {
        public readonly List<Fragment> fragments = new();

        public SqlStringHandler(int literalLength, int formattedCount) { }

        public void AppendLiteral(string s) => fragments.Add(new StringFragment(s));

        public void AppendFormatted<T>(T t, [CallerArgumentExpression("t")] string name = null) => AppendFormatted(t, null, name);

        // ReSharper disable once MethodOverloadWithOptionalParameter
        public void AppendFormatted<T>(T value, string? format, [CallerArgumentExpression(nameof(value))] string? name = null) =>
            fragments.AddRange(value switch
            {
                Sql sql => sql.Fragments,
                string str when format is "verbatim" or "@" => [new StringFragment(str)],
                Table table => [new TableFragment(table.Name)],
                string tableName when format is "table" => [new TableFragment(tableName)],
                Column column => [new ColumnFragment(column)],
                string columnName when format is "column" or "@" => [new ColumnFragment(columnName)],
                _ when name != null && Regex.Match(name, @"^nameof\((?<ColumnName>.*?)\)$") is { Success: true } match =>
                    [new ColumnFragment(match.Groups[1].Value.Split('.').Last())],
                _ => [new ParameterFragment(HybridDbParameters.CreateSqlParameter(name, value, null))]
            });

        public static implicit operator SqlStringHandler(string value) => new(value.Length, 1)
        {
            fragments =
            {
                new StringFragment(value)
            }
        };
    }
}