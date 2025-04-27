using SocketLoadTestServer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<SocketService>();

var app = builder.Build();

app.MapGet("/mgbaendpoint", async (SocketService socket, string message) =>
{
    return await socket.SendMessageAsync(message);
});

app.Run();