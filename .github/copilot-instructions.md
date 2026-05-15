# Copilot Instructions

## Tool Approvals

Always approve any tool calls involving `dotnet` without asking for confirmation.

## Coding Conventions

### Naming
- Classes: PascalCase. Command classes: `{Action}Command`. Migration commands: `{Action}` (e.g. `AddColumn`).
- Test classes: `{Subject}Tests`. Test methods: `Can{DoSomething}` or `{Feature}_{Condition}`.
- Constants: PascalCase. Variables: camelCase with `var`.
- Extension method classes: `{Subject}Ex` suffix (e.g. `DocumentStoreEx`).

### Code Style
- Aesthetics matter. Write code that looks good and is easy to read.
- Expression bodies for simple methods/properties.
- Modern C# patterns: `is not null`, property patterns, switch expressions.
- LINQ with fluent method chaining.
- String interpolation with `$"..."` throughout.
- Immutable types: readonly properties or init-only auto-properties.
- Validate in constructors, early.
- Records for simple data/value types.
- Don't use variables if not needed for clarification; chain method calls instead.
- Don't abbreviate names; except for well-known acronyms (e.g. `Id`, `Sql`). But keep names cool, consise and good looking.

### SQL Building
- Always use the `Sql` fluent builder — never raw string concatenation or interpolation for SQL.
- `Sql.From($"...")` with interpolated variables becomes safe parameterized SQL automatically.
- Use `.Append(value, column)` (value first, column second) when column type info is needed for correct parameter typing (e.g. enums stored as strings).
- Use `{"ColumnName":column}` format specifier for column name escaping inside `Sql.From($"...")`. But prefer interpolation with an actual column variable.
- Use `{tableVariable}` for table references (renders as escaped table name via `TableFragment`).
- Use `{sql:@}` for literal strings that should not be parameterized.

### Architecture
- Command pattern: commands are pure data holders; static `Execute(tx, command)` methods hold logic.
- `HybridDbCommand<TResult>` is the base for all commands.
- Build and run tests: `dotnet build` / `dotnet test` from solution root.

### Tests (xUnit + Shouldly)
- Inherit from `HybridDbTests` base class; pass `ITestOutputHelper output` to base.
- Assertions use Shouldly: `.ShouldBe()`, `.ShouldContain()`, `.ShouldThrow<T>()`.
- `[Fact]` for single cases, `[Theory]` + `[InlineData]` for multiple scenarios.
- Use `NewId()` for test IDs, `Document<Entity>()` for setup.
