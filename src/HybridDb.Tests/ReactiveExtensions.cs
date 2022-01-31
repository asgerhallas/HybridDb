using System;
using System.Reactive.Linq;
using Xunit;

namespace HybridDb.Tests
{
    public class ReactiveExtensions
    {
        [Fact]
        public void Observe()
        {
            var observable = Observable.Range(1, 10);
            observable.Subscribe(Console.WriteLine);
        }
    }
}