using Microsoft.Extensions.DependencyInjection;

namespace Bob1.Tests;

public class DomainEventDispatcherTests
{
    private sealed record PingEvent(string Text) : DomainEvent;

    private sealed class RecordingHandler : IDomainEventHandler<PingEvent>
    {
        public List<string> Received { get; } = [];

        public Task HandleAsync(PingEvent domainEvent, CancellationToken ct)
        {
            Received.Add(domainEvent.Text);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHandler : IDomainEventHandler<PingEvent>
    {
        // Throws synchronously on purpose: the dispatcher calls handlers by reflection and must not leak TargetInvocationException.
        public Task HandleAsync(PingEvent domainEvent, CancellationToken ct) =>
            throw new InvalidOperationException("handler failed");
    }

    private static DomainEventDispatcher Build(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        register(services);
        return new DomainEventDispatcher(services.BuildServiceProvider());
    }

    [Fact]
    public async Task Delivers_each_event_to_its_handlers_in_order()
    {
        var handler = new RecordingHandler();
        var dispatcher = Build(s => s.AddSingleton<IDomainEventHandler<PingEvent>>(handler));

        await dispatcher.DispatchAsync([new PingEvent("one"), new PingEvent("two")], CancellationToken.None);

        Assert.Equal(new[] { "one", "two" }, handler.Received);
    }

    [Fact]
    public async Task Calls_every_registered_handler()
    {
        var first = new RecordingHandler();
        var second = new RecordingHandler();
        var dispatcher = Build(s =>
        {
            s.AddSingleton<IDomainEventHandler<PingEvent>>(first);
            s.AddSingleton<IDomainEventHandler<PingEvent>>(second);
        });

        await dispatcher.DispatchAsync([new PingEvent("hello")], CancellationToken.None);

        Assert.Equal(new[] { "hello" }, first.Received);
        Assert.Equal(new[] { "hello" }, second.Received);
    }

    [Fact]
    public async Task An_event_without_handlers_is_a_no_op()
    {
        var dispatcher = Build(_ => { });

        await dispatcher.DispatchAsync([new PingEvent("nobody listens")], CancellationToken.None);
    }

    [Fact]
    public async Task A_failing_handler_surfaces_the_original_exception()
    {
        var dispatcher = Build(s => s.AddSingleton<IDomainEventHandler<PingEvent>>(new ThrowingHandler()));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.DispatchAsync([new PingEvent("boom")], CancellationToken.None));

        Assert.Equal("handler failed", ex.Message);
    }

    [Fact]
    public void Events_get_a_unique_id_and_a_utc_timestamp()
    {
        var a = new PingEvent("a");
        var b = new PingEvent("b");

        Assert.NotEqual(a.EventId, b.EventId);
        Assert.Equal(DateTimeKind.Utc, a.OccurredOnUtc.Kind);
    }
}
