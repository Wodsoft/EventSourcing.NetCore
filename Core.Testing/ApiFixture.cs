using System.Linq.Expressions;
using Core.Api.Testing;
using Core.Events;
using Core.Events.External;
using Core.Requests;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Core.Testing;

public class TestWebApplicationFactory<TProject>: WebApplicationFactory<TProject> where TProject : class
{
    private readonly EventsLog eventsLog = new();
    private readonly DummyExternalEventProducer externalEventProducer = new();
    private readonly DummyExternalCommandBus externalCommandBus = new();

    private readonly string schemaName = Guid.NewGuid().ToString("N").ToLower();

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton(eventsLog)
                .AddSingleton(typeof(IEventHandler<>), typeof(EventListener<>))
                .AddSingleton<IExternalEventProducer>(externalEventProducer)
                .AddSingleton<IEventBus>(sp =>
                new EventBusDecoratorWithExternalProducer(sp.GetRequiredService<EventBus>(),
                    sp.GetRequiredService<IExternalEventProducer>()))
                .AddSingleton<IExternalCommandBus>(externalCommandBus)
                .AddSingleton<IExternalEventConsumer, DummyExternalEventConsumer>();
        });


        Environment.SetEnvironmentVariable("SchemaName", schemaName);

        return base.CreateHost(builder);
    }

    public void PublishedExternalEventsOfType<TEvent>() where TEvent : IExternalEvent =>
        externalEventProducer.PublishedEvents.OfType<TEvent>().ToList().Should().NotBeEmpty();

    public async Task PublishInternalEvent(object @event, CancellationToken ct = default)
    {
        using var scope = Services.CreateScope();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        await eventBus.Publish(@event, ct);
    }

    public IReadOnlyCollection<TEvent> PublishedInternalEventsOfType<TEvent>() =>
        eventsLog.PublishedEvents.OfType<TEvent>().ToList();

    public async Task ShouldPublishInternalEventOfType<TEvent>(
        Expression<Func<TEvent, bool>> predicate,
        int maxNumberOfRetries = 5,
        int retryIntervalInMs = 1000)
    {
        var retryCount = maxNumberOfRetries;
        var finished = false;

        do
        {
            try
            {
                PublishedInternalEventsOfType<TEvent>().Should()
                    .HaveCount(1)
                    .And.Contain(predicate);

                finished = true;
            }
            catch
            {
                if (retryCount == 0)
                    throw;
            }

            await Task.Delay(retryIntervalInMs);
            retryCount--;
        } while (!finished);
    }
}

// public abstract class ApiWithEventsFixture<TProject>: ApiFixture<TProject> where TProject : class
// {
//     private readonly EventsLog eventsLog = new();
//     private readonly DummyExternalEventProducer externalEventProducer = new();
//     private readonly DummyExternalCommandBus externalCommandBus = new();
//
//     public override TestContext<TProject> CreateTestContext() =>
//         new(services =>
//         {
//             SetupServices?.Invoke(services);
//             services.AddSingleton(eventsLog);
//             services.AddSingleton(typeof(IEventHandler<>), typeof(EventListener<>));
//             services.AddSingleton<IExternalEventProducer>(externalEventProducer);
//             services.AddSingleton<IEventBus>(sp =>
//                 new EventBusDecoratorWithExternalProducer(sp.GetRequiredService<EventBus>(),
//                     sp.GetRequiredService<IExternalEventProducer>()));
//             services.AddSingleton<IExternalCommandBus>(externalCommandBus);
//             services.AddSingleton<IExternalEventConsumer, DummyExternalEventConsumer>();
//         });
//
//
//     public void PublishedExternalEventsOfType<TEvent>() where TEvent : IExternalEvent
//     {
//         externalEventProducer.PublishedEvents.OfType<TEvent>().ToList().Should().NotBeEmpty();
//     }
//
//     public async Task PublishInternalEvent(object @event, CancellationToken ct = default)
//     {
//         using var scope = Sut.Services.CreateScope();
//         var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
//         await eventBus.Publish(@event, ct);
//     }
//
//     public IReadOnlyCollection<TEvent> PublishedInternalEventsOfType<TEvent>() =>
//         eventsLog.PublishedEvents.OfType<TEvent>().ToList();
//
//     // TODO: Add Poly here
//     public async Task ShouldPublishInternalEventOfType<TEvent>(
//         Expression<Func<TEvent, bool>> predicate,
//         int maxNumberOfRetries = 5,
//         int retryIntervalInMs = 1000)
//     {
//         var retryCount = maxNumberOfRetries;
//         var finished = false;
//
//         do
//         {
//             try
//             {
//                 PublishedInternalEventsOfType<TEvent>().Should()
//                     .HaveCount(1)
//                     .And.Contain(predicate);
//
//                 finished = true;
//             }
//             catch
//             {
//                 if (retryCount == 0)
//                     throw;
//             }
//
//             await Task.Delay(retryIntervalInMs);
//             retryCount--;
//         } while (!finished);
//     }
// }
