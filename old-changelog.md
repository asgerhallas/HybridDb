Changelog
=========

Note that later version are added as releases here on Github (https://github.com/asgerhallas/HybridDb/releases) and the changelog is available for each relase.

#### 0.8.3

- @afkpost added support for separate document metadata

#### 0.8.4

- @mookid8000 fixed issue #21

#### 0.8.5

- Added support for persisting anonynomous types
- Added support for persisting non-configured types in an always present Document table
- Added TypeMapper instead of requiring configuration for all concrete type manually

#### 0.9.0

- Changed the configuration api to accomodate ad hoc document setup. Now you need to call store.Initialize() after configuration to run the migrations.

#### 0.9.1

- Fixed issue with decimal-columns not having any scale. Now they are set to 28, 14.

#### 0.9.2

- Fixed issue with deadlocks while migrating schema in parallel test executing using temp tables
- Made deadlocks less likely for real tables too
- Changed id uniqueness in session to be table+key instead of type+key.

#### 0.9.3

- Added overload for DocumentStore.Create with configuration action

#### 0.9.4

- Added configuration option to set the default key resolver, which is used when trying to obtain an id from an entity in store.
- The default key resolver (if non other is set) now calls ToString() by default on the automatically resolved id from the stored entity. So if you use a Guid it will ToString it for you when you store.

#### 0.9.5

- Fixes issue #24
- Enforce store initialization before first session is opened

#### 0.10.0

- Changes default nvarchar length to 1024 with support for overriding this default like `store.Configuration.Document<Doc>.With(x => x.StringProp, new MaxLength(2048));`. Fixes issue #20.

#### 0.10.1

- @chessydk optimized the sql for getting total rows of non-windowed queries (see #34).

#### 0.10.2

- Internal change to metadata API

#### 0.10.3

- Fix for bug that prevented queries on anonymous types

#### 0.10.4

- Fix for bug that prevented queries on unconfigured types

#### 0.10.5

- Upgrade of Serilog and Newtonsoft.Json (thanks to @mookid8000 / https://github.com/asgerhallas/HybridDb/pull/36)

#### 0.10.6

- Better handling of queries on unconfigured types and ad-hoc configuring

#### 0.10.7

- Fixing handling of queries on unconfigured types and ad-hoc configuring for queries with projections

#### 0.10.8

- ILMerging the Indentional lib

#### 0.10.9

- Fixing bug that made SQL Server fail with a "too many parameters" error. The documented threshold i 2100 params, but it failed when given 2099 params.

#### 0.10.10

- Some fixes for appveyor setup

#### 0.10.11

- Merged Indentional.dll

#### 0.10.23

- Upgraded Dapper
- Fixed pre-migration backups

#### 0.10.24

- Upgraded Newtonsoft.Json to 10.0.3

#### 0.10.25

- Fixed pre-migration backup, such that it only backups when there's an actual migration
- Bumped logging of some informations down to debug levels

#### 0.10.26

- Fixed missing disposal of connections that did not open correctly.

#### 0.10.27

- Stop using CTE for non-windowed queries to gain performance.

#### 0.10.35

- Add Upsert command to DocumentStore

#### 0.10.36

- Fixing deadlock for schema migrations of temp tables (conccurent tests failed)
