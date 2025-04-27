using SocketLoadTestServer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<SocketService>();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapGet("/mgbaendpoint", async (SocketService socket, string message) =>
{
    return await socket.SendMessageAsync(message);
});

app.Lifetime.ApplicationStopping.Register(() =>
{
    Console.Beep();
});

AppDomain.CurrentDomain.ProcessExit += (s, e) =>
{
    Console.Beep();
};

app.Run();