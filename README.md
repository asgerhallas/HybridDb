[![Build](https://github.com/asgerhallas/HybridDb/actions/workflows/dotnet.yml/badge.svg?branch=master)](https://github.com/asgerhallas/HybridDb/actions/workflows/dotnet.yml)
[![NuGet](https://img.shields.io/nuget/v/HybridDb.svg)](https://www.nuget.org/packages/HybridDb)

HybridDb
========

HybridDb makes it easy to store and query semi-structured data and documents in SQL Server.

**[📚 Read the Documentation](docs/Home.md)** to get started quickly.

HybridDb gives you:

- An opinionated and very easy to use Unit of Work API
- Easy schemaless - and mostly mappingless - persistance of any .NET object
- A few simple indexing features that enables querying on document properties
- A LINQ provider for mentioned queries and an SqlBuilder for the advanced stuff 
- All the consistency, transactionality and performance of Sql Server
- Migration tools for document and index changes
- A message queue that integrates seamlessly (and consistently) with the rest

HybridDb aims to be a small and focused library with no magic and no surprises.

So if all you need is to put some JSON to rest - on a single server, in simple manner - HybridDb might be the tool for you.

How?
====

Like this:

    var store = DocumentStore.ForTesting(TableMode.TempTables);

    store.Document<Entity>().With(x => x.Property);

    using (var session = store.OpenSession())
    {
        session.Store(new Entity { 
            Id = Guid.NewGuid(), 
            Property = 1999, 
            Field = "New document" 
        });

        session.SaveChanges();
    }

    using (var session = store.OpenSession())
    {
        var existingEntity = session.Query<Entity>().Single(x => x.Property < 2000);

        existingEntity.Property++;

        session.SaveChanges();
    }

For more information, see the [documentation](docs/Home.md).

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
