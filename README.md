[![Build status](https://ci.appveyor.com/api/projects/status/ud71up5svo5mhcas/branch/master?svg=true)](https://ci.appveyor.com/project/asgerhallas/hybriddb/branch/master)

HybridDb
========

HybridDb makes it easy to store and query semi-structured data and documents in SQL Server (or other relational databases when the time comes).

HybridDb gives you:

- A unit of work API similar to NHibernate or RavenDB
- Easy schemaless and mostly mappingless persistance of any .NET object
- A few simple indexing features that enables querying on document properties
- A LINQ provider for mentioned queries
- All the consistency and transactionality of a relational database
- Migration tools for document changes and index changes
- A nifty little document viewer

HybridDb aims to be a small and focused library with no magic and no surprises.

So if all you need is to put some JSON to rest - on a single server, in simple manner - HybridDb might be the tool for you.

Why?
====

We have been happy users of the [awesome RavenDB](http://ravendb.net/) for a quite a while, 
but we ran into some performance issues that we were not able to dodge ([read more here](https://groups.google.com/d/topic/ravendb/6NjiJpzYxyI/discussion)).

With no solution to the problem in sight - and in the lucky situation that we did not rely on any of RavenDB's more advanced features - we decided to write a drop-in replacement to run on top of SQL Server.

HybridDb is the result of this effort and fulfills our modest requirements - and we hope it can be useful for others too.

How?
====

Like this:

    var store = DocumentStore.ForTesting(TableMode.TempTables);
    store.Document<Entity>().With(x => x.Property);
    
    using (var session = store.OpenSession())
    {
        session.Store(new Entity { Id = Guid.NewGuid(), Property = 2001, Field = "Hello" });
        session.SaveChanges();
    }
    
    using (var session = store.OpenSession())
    {
        var entity = session.Query<Entity>().Single(x => x.Property > 2000);
        entity.Property++;
        session.SaveChanges();
    }

Changelog
=========

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


Acknowledgements
================

HybridDb uses [Dapper](http://code.google.com/p/dapper-dot-net/), [Json.NET](http://http://json.net/) and [Inflector](https://github.com/srkirkland/Inflector) ... all of which are brilliant pieces of open source software.

Code for managing anonymous objects is taken [from here](http://blog.andreloker.de/post/2008/05/03/Anonymous-type-to-dictionary-using-DynamicMethod.aspx)

[NHibernate](http://nhforge.org/) and [RavenDB](http://ravendb.net/) has been our role models.

So that's it... HybridDb is standing on the shoulder of giants. And it can't get down.

License
=======

Copyright (C) 2013 Lars Udengaard, Jacob Bach Pedersen and Asger Hallas.

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
