using System;
using System.Threading.Tasks;
using Conduit.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

// Keep for now, though File.Delete might be removed for InMemory
// Assuming ConduitContext is here

// Required for RemoveAll

namespace Conduit.IntegrationTests;

public class SliceFixture : IDisposable
{
    private readonly string _dbName = Guid.NewGuid() + ".db"; // Unique name for InMemory DB
    private readonly ServiceProvider _provider;
    private readonly IServiceScopeFactory _scopeFactory;

    public SliceFixture()
    {
        var services = new ServiceCollection();
        services.AddConduit();

        services.RemoveAll<DbContextOptions<ConduitContext>>();
        services.RemoveAll<ConduitContext>();

        var dbContextOptions = new DbContextOptionsBuilder<ConduitContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        services.AddSingleton(dbContextOptions);

        services.AddScoped<ConduitContext>();


        _provider = services.BuildServiceProvider();

        using (var scope = _provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ConduitContext>();
            context.Database.EnsureCreated();
        }

        _scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();
    }

    public void Dispose()
    {
    }

    public ConduitContext GetDbContext()
    {
        var scope = _provider
            .CreateScope();
        return scope.ServiceProvider.GetRequiredService<ConduitContext>();
    }

    public async Task ExecuteScopeAsync(Func<IServiceProvider, Task> action)
    {
        using var scope = _scopeFactory.CreateScope();
        await action(scope.ServiceProvider);
    }

    public async Task<T> ExecuteScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = _scopeFactory.CreateScope();
        return await action(scope.ServiceProvider);
    }

    public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request) =>
        ExecuteScopeAsync(sp =>
        {
            var mediator = sp.GetRequiredService<IMediator>();
            return mediator.Send(request);
        });

    public Task SendAsync(IRequest request) =>
        ExecuteScopeAsync(sp =>
        {
            var mediator = sp.GetRequiredService<IMediator>();
            return mediator.Send(request);
        });

    public Task ExecuteDbContextAsync(Func<ConduitContext, Task> action) =>
        ExecuteScopeAsync(sp => action(sp.GetRequiredService<ConduitContext>()));

    public Task<T> ExecuteDbContextAsync<T>(Func<ConduitContext, Task<T>> action) =>
        ExecuteScopeAsync(sp => action(sp.GetRequiredService<ConduitContext>()));

    public Task InsertAsync(params object[] entities) =>
        ExecuteDbContextAsync(db =>
        {
            foreach (var entity in entities)
            {
                db.Add(entity);
            }

            return db.SaveChangesAsync();
        });
}
