using System.Threading.Tasks;
using FakeItEasy;
using HybridDb.Queue;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Queue
{
    public class MessageHandlerDispatcherTests
    {
        public record MyMessage(string Text);

        [Fact]
        public async Task DispatchesToCorrectHandler()
        {
            var handler = A.Fake<IMessageHandler<MyMessage>>();
            var session = A.Fake<IDocumentSession>();
            var message = new HybridDbMessage("id-1", new MyMessage("hello"));

            var dispatch = MessageHandlerDispatcher.For(type =>
                type == typeof(IMessageHandler<MyMessage>)
                    ? new object[] { handler }
                    : []);

            await dispatch(session, message);

            A.CallTo(() => handler.Handle(session, new MyMessage("hello"))).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task DispatchesToMultipleHandlers()
        {
            var handler1 = A.Fake<IMessageHandler<MyMessage>>();
            var handler2 = A.Fake<IMessageHandler<MyMessage>>();
            var session = A.Fake<IDocumentSession>();
            var message = new HybridDbMessage("id-2", new MyMessage("world"));

            var dispatch = MessageHandlerDispatcher.For(_ => new object[] { handler1, handler2 });

            await dispatch(session, message);

            A.CallTo(() => handler1.Handle(session, new MyMessage("world"))).MustHaveHappenedOnceExactly();
            A.CallTo(() => handler2.Handle(session, new MyMessage("world"))).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task NoHandlers_IsNoop()
        {
            var session = A.Fake<IDocumentSession>();
            var message = new HybridDbMessage("id-3", new MyMessage("noop"));

            var dispatch = MessageHandlerDispatcher.For(_ => []);

            var exception = await Record.ExceptionAsync(() => dispatch(session, message));

            exception.ShouldBeNull();
        }

        [Fact]
        public async Task PassesSessionToHandler()
        {
            var capturedSession = default(IDocumentSession);
            var session = A.Fake<IDocumentSession>();
            var message = new HybridDbMessage("id-4", new MyMessage("session-check"));

            var handler = A.Fake<IMessageHandler<MyMessage>>();
            A.CallTo(() => handler.Handle(A<IDocumentSession>._, A<MyMessage>._))
                .Invokes(call => capturedSession = call.GetArgument<IDocumentSession>(0))
                .Returns(Task.CompletedTask);

            var dispatch = MessageHandlerDispatcher.For(_ => new object[] { handler });

            await dispatch(session, message);

            capturedSession.ShouldBeSameAs(session);
        }
    }
}
