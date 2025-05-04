using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.ObjectPool;
using SocketLoadTestServer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.TryAddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
builder.Services.TryAddSingleton(serviceProvider =>
{
    var provider = serviceProvider.GetRequiredService<ObjectPoolProvider>();
    var policy = new ReusableSocketPooledObjectPolicy("127.0.0.1", 8888);
    return provider.Create(policy);
});
//builder.Services.AddSingleton<SocketService>();

var app = builder.Build();

//app.UseHttpsRedirection();

app.MapGet("/mgbaendpoint", async (ObjectPool<ReusableSocket> socketPool, string message) =>
{
    var socket = socketPool.Get();

    try
    {
        return await socket.SendMessageAsync(message);
    }
    finally
    {
        socketPool.Return(socket);
    }
});

app.Run();