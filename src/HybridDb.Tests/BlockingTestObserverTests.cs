using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using HybridDb.Linq.Bonsai;
using HybridDb.Queue;
using ShouldBeLike;
using Xunit;

namespace HybridDb.Tests
{
    public class BlockingTestObserverTests
    {
        [Fact]
        public async Task Test()
        {
            var observer = new BlockingTestObserver(TimeSpan.FromSeconds(10));

            var subject = Observable.Create<object>(async x =>
            {
                //await Task.Yield();

                x.OnNext(new One());

                await Task.Delay(TimeSpan.FromSeconds(1));

                x.OnNext(new Two());
                x.OnNext(new Three());
                
                return Disposable.Empty;
            });

            using var disposable = observer.Subscribe(subject.ObserveOn(NewThreadScheduler.Default), CancellationToken.None);

            //events.ShouldBeLike(new One());

            await observer.AdvanceUntil<Two>();

            //events.ShouldBeLike(new One(), new Two());
        }

        public record One;
        public record Two;
        public record Three;
        public record Four;
    }
}