# How to Build and Contribute

## Prerequisites

To build and contribute to HybridDb, you'll need:

- **.NET SDK**: .NET 6.0 or later
- **SQL Server**: SQL Server 2016 or later (or SQL Server LocalDB/Express)
- **IDE**: Visual Studio 2022, VS Code, or Rider
- **Git**: For version control

## Getting the Source Code

Clone the repository:

```bash
git clone https://github.com/asgerhallas/HybridDb.git
cd HybridDb
```

## Building the Project

### Using the Command Line

Build the solution:

```bash
dotnet build HybridDb.sln
```

Run tests:

```bash
dotnet test HybridDb.sln
```

### Using Visual Studio

1. Open `HybridDb.sln` in Visual Studio
2. Build the solution (Ctrl+Shift+B)
3. Run tests using Test Explorer (Ctrl+E, T)

## Project Structure

```
HybridDb/
├── src/
│   ├── HybridDb/                    # Core library
│   │   ├── Commands/                # Database command implementations
│   │   ├── Config/                  # Configuration and design
│   │   ├── Events/                  # Event store functionality
│   │   ├── Linq/                    # LINQ query provider
│   │   ├── Migrations/              # Schema and document migrations
│   │   ├── Queue/                   # Message queue implementation
│   │   └── Serialization/           # Serialization abstractions
│   └── HybridDb.Tests/              # Test suite
├── tools/                           # Build and deployment tools
├── HybridDb.sln                     # Solution file
└── README.md
```

## Running Tests

### All Tests

```bash
dotnet test
```

### Test Database

Tests use SQL Server LocalDB with temp tables by default. LocalDB is a lightweight version of SQL Server that's perfect for development and testing.

The default connection string connects to:
- Server: `(LocalDB)\MSSQLLocalDB`
- Authentication: Integrated Security (Windows Authentication)

To use a different SQL Server instance, modify the `GetConnectionString()` method in [HybridDbTests.cs](../src/HybridDb.Tests/HybridDbTests.cs#L70).

## Development Guidelines

### Code Style

- Follow existing code style and conventions
- Use meaningful variable and method names
- Keep methods focused and concise
- Add XML documentation comments for public APIs

### Testing

- Write tests for new features using the HybridDbTests base class and helper methods
- Ensure existing tests pass before submitting
- Use descriptive test names that explains the setup and the act part (don't include the assertion in the name)
- Follow the Arrange-Act-Assert pattern, but don't write Arrange-Act-Assert comments

Example:

<!-- snippet: CanStoreAndLoadEntity -->
<a id='snippet-CanStoreAndLoadEntity'></a>

```cs
Document<Entity>();

var entity = new Entity { Id = NewId(), Property = "Test" };

using var session1 = store.OpenSession();

session1.Store(entity);
session1.SaveChanges();

using var session2 = store.OpenSession();

session2.Load<Entity>(entity.Id)
    .Property.ShouldBe("Test");
```
<sup><a href='/src/HybridDb.Tests/Documentation/Doc02_ContributingTests.cs#L16-L30' title='Snippet source file'>snippet source</a> | <a href='#snippet-CanStoreAndLoadEntity' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Commit Messages

Write clear, descriptive commit messages:

- Use present tense ("Add feature" not "Added feature")
- First line should be a summary (50 chars or less)
- Add detailed description if needed after a blank line

Example:
```
Add support for DateOnly type

- Implement DateOnlyTypeHandler for Dapper
- Add tests for DateOnly serialization
- Update documentation
```

## Contributing

### Workflow

1. **Fork the repository** on GitHub
2. **Create a feature branch** from `master`:
   ```bash
   git checkout -b feature/my-new-feature
   ```
3. **Make your changes** and commit them
4. **Write or update tests** for your changes
5. **Ensure all tests pass**
6. **Push to your fork**:
   ```bash
   git push origin feature/my-new-feature
   ```
7. **Submit a Pull Request** on GitHub

### Pull Request Guidelines

- Describe what the PR does and why
- Reference any related issues
- Ensure CI builds pass
- Be responsive to feedback and reviews
- Keep PRs focused on a single feature or fix

### What to Contribute

We welcome contributions in several areas:

- **Bug fixes**: Report and fix bugs
- **Features**: Add new functionality
- **Documentation**: Improve or expand documentation
- **Tests**: Add test coverage
- **Performance**: Optimize existing code
- **Examples**: Add usage examples

## Debugging

## Building NuGet Packages

To create NuGet packages locally:

```bash
dotnet pack src/HybridDb/HybridDb.csproj -c Release
```

The package will be created in `src/HybridDb/bin/Release/`.

Merged pull requests and new features will be released to nuget.org by @asgerhallas.

## Continuous Integration

HybridDb uses GitHub Actions for continuous integration. The build runs on every push to master and every pull request to ensure code quality and compatibility. 

Please check you pull request for build status and fix it if the build fails.

## Getting Help

- **Issues**: Check [GitHub Issues](https://github.com/asgerhallas/HybridDb/issues) for known issues or to report bugs
- **Discussions**: Start a discussion for questions or feature requests
- **Code Review**: Submit a draft PR for early feedback on larger changes

## License

HybridDb is licensed under the MIT License. By contributing, you agree that your contributions will be licensed under the same license.

## Acknowledgments

HybridDb builds upon several excellent open-source projects:

- **[Dapper](https://github.com/DapperLib/Dapper)**: Micro ORM for .NET
- **[Json.NET](https://www.newtonsoft.com/json)**: JSON serialization
- **[Inflector](https://github.com/srkirkland/Inflector)**: String pluralization

Special thanks to [NHibernate](https://nhibernate.info/) and [RavenDB](https://ravendb.net/) for inspiring the API design.
