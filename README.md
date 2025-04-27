# mGBA-lua-socket-loadtest

This work continues from [mGBA-lua-Socket](https://github.com/nikouu/mGBA-lua-Socket) in order to hone the socket functionality used in [mGBA-http](https://github.com/nikouu/mGBA-http).

This project has three performance goals: 
1. Find the cleanest way to connect and disconnect sockets
2. Find the throughput limits
3. Explore multiplexing

## Preface

This document will go through each Git tag milestone to explain in depth what's being explored and why. 

The structure of the work loosely matches mGBA-http with an HTTP endpoint that calls an injected `SocketService` object.

When the .NET server is running, the message can be sent via:
```
https://localhost:7185/mgbaendpoint?message=a
```

[heroldev/AGB-buttontest](https://github.com/heroldev/AGB-buttontest) is the ROM used when running mGBA.

# Version 1

[Tag link](https://github.com/nikouu/mGBA-lua-socket-loadtest/tree/Version1)

Version 1 brings starts this project with:
1. Singleton `SocketService`
2. Lua script from [mGBA-lua-Socket](https://github.com/nikouu/mGBA-lua-Socket) modified to reflect back the given message

While there will be issues when sending many messages at once due to the singleton socket, this version will only deal with simple messages now and again.

# Version 2

[Tag link](https://github.com/nikouu/mGBA-lua-socket-loadtest/tree/Version2)

## Handling socket cleanup on close

This version begins to look at how to deal with the different ways mGBA-http can be closed.

When closing the server, the message from mGBA is inconsistent. I'm unsure if it's because the IDisposable of `SocketServer` isn't correctly run on shutdown or if there is something else. This is a problem in mGBA-http. The message on closing the server will be either:
1. [ERROR] Socket 1 Error: disconnected
1. [ERROR] Socket 1 Error: unknown error

Either one happens after a connect-request-disconnect cycle. However closing the debugged application via the stop in Visual Studio more often does the "disconnected" message (or via Ctrl + C in the console), whereas closing the console window with the "x" seems to mostly do the "unknown error" message.

![Version 1 disconnected](images/version1_disconnected.jpg)
![Version 1 unknown error](images/version1_unknownError.jpg)

I assume it's something to do with how the OS./NET sends kill/close messages to the process when using different close methods. Looking into it:

- [Host shutdown](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host?tabs=appbuilder#host-shutdown)
- [Host shutdown in web server scenarios](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host?tabs=appbuilder#host-shutdown-in-web-server-scenarios)
- [Web Host shutdown](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/web-host?view=aspnetcore-9.0#shutdown-timeout)
- [WebApplication and WebApplicationBuilder in Minimal API apps](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/webapplication?view=aspnetcore-9.0)
- [Detecting console closing in .NET](https://www.meziantou.net/detecting-console-closing-in-dotnet.htm)
- [Closing of TCP socket - What is different when connection is closed by debugger](https://stackoverflow.com/questions/24281037/closing-of-tcp-socket-what-is-different-when-connection-is-closed-by-debugger)
- [Extending the shutdown timeout setting to ensure graceful IHostedService shutdown](https://andrewlock.net/extending-the-shutdown-timeout-setting-to-ensure-graceful-ihostedservice-shutdown/)
- [What is the difference between the SIGINT and SIGTERM signals in Linux? What’s the difference between the SIGKILL and SIGSTOP signals?](https://www.quora.com/What-is-the-difference-between-the-SIGINT-and-SIGTERM-signals-in-Linux-What%E2%80%99s-the-difference-between-the-SIGKILL-and-SIGSTOP-signals?share=1)
- [Can I handle the killing of my windows process through the Task Manager?](https://stackoverflow.com/questions/1527450/can-i-handle-the-killing-of-my-windows-process-through-the-task-manager)
- [Closing the Window](https://learn.microsoft.com/en-us/windows/win32/learnwin32/closing-the-window)
- [How to distinguish 'Window close button clicked (X)' vs. window.Close() in closing handler](https://stackoverflow.com/questions/13361260/how-to-distinguish-window-close-button-clicked-x-vs-window-close-in-closi/20006210#20006210)

The `Dispose()` method is called when Ctrl + C is used. But doesn't seem to get called with the other closure methods. Which now seems odd bceause we get into this state:

| Closure method    | `Dispose()` runs? | `Lifetime.ApplicationStopping` runs? | mGBA socket close                                |
| ----------------- | ----------------- | ------------------------------------ | ------------------------------------------------ |
| Ctrl + C          | Yes               | Yes                                  | "disconnected"                                   |
| Close button      | No                | No                                   | "unknown error"                                  |
| VS stop debugging | No                | No                                   | Mostly "disconnected", sometimes "unknown error" |

_Technically closure meathods can also be application close or application kill from the Task Manager or command prompt._

I anticipate that most people will close mGBA-http with the close "x" button meaning it will be worth better understanding how the shutdown works in that case. Though it seems that even with these exit hooks, the socket can't be closed cleanly:

```csharp
// 1. Relying on the usual hooks to dispose

// 2. Using Lifetime.ApplicationStopping
app.Lifetime.ApplicationStopping.Register(() =>
{

});

// 3. Using the ProcessExit callback
AppDomain.CurrentDomain.ProcessExit += (s, e) => {

};
```

Ultimately then this probably has to be handled in the Lua script to deal with abrupt socket closures that lead to "unknown error". This can be updated by adding:
```lua
if err ~= socket.ERRORS.AGAIN then
	console:log("ST_received 5")
	if err == "disconnected" then
		console:log("ST_received 6")
		console:log(ST_format(id, err, true))
	elseif err == socket.ERRORS.UNKNOWN_ERROR then
		console:log("ST_received 7")
		console:log(ST_format(id, err, true))
	else
		console:log("ST_received 8")
		console:error(formatMessage(id, err, true))
	end
	ST_stop(id)
end
```

Where we can have the option of swallowing the error.

## socket.ERRORS.AGAIN

It seems every request will have the "socket.ERRORS.AGAIN" error. Even the two example lua scripts [[1](https://github.com/mgba-emu/mgba/blob/c33a0d65344984294ed8666e98d1735a29f0a2d8/res/scripts/socketserver.lua#L37)][[2](https://github.com/mgba-emu/mgba/blob/c33a0d65344984294ed8666e98d1735a29f0a2d8/res/scripts/sockettest.lua#L39)] from the mGBA repo ignore this error. Meaning this can ignore it too.

# Version 3

[Tag link](https://github.com/nikouu/mGBA-lua-socket-loadtest/tree/Version3)

Version 3 is around manually cleaning up a socket. Ensuring that future work can be correctly cleaned up outside of the `Dispose()` call in `SocketService.cs`. 

For this, a new socket will be created inside the `SocketService` singleton for each request then cleaned up afterwards. This can automatically be done with `using`:

```csharp
public async Task<string> SendMessageAsync(string message)
{
    var ipAddress = IPAddress.Parse("127.0.0.1");
    var ipEndpoint = new IPEndPoint(ipAddress, 8888);
    using var socket = new Socket(ipEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp); // using declaration for automatic cleanup

    await socket.ConnectAsync(ipEndpoint);

    var messageBytes = Encoding.UTF8.GetBytes(message);
    await socket.SendAsync(messageBytes, SocketFlags.None);

    var buffer = new byte[1_024];
    var received = await socket.ReceiveAsync(buffer, SocketFlags.None);
    var response = Encoding.UTF8.GetString(buffer, 0, received);

    return response;
}
```

Which correctly triggers the "disconnected" status in mGBA.

But to better understand the situation we can clean up the socket ourselves:

```csharp
public async Task<string> SendMessageAsync(string message)
{
    var ipAddress = IPAddress.Parse("127.0.0.1");
    var ipEndpoint = new IPEndPoint(ipAddress, 8888);
    var socket = new Socket(ipEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

    await socket.ConnectAsync(ipEndpoint);

    var messageBytes = Encoding.UTF8.GetBytes(message);
    await socket.SendAsync(messageBytes, SocketFlags.None);

    var buffer = new byte[1_024];
    var received = await socket.ReceiveAsync(buffer, SocketFlags.None);
    var response = Encoding.UTF8.GetString(buffer, 0, received);

    socket.Shutdown(SocketShutdown.Both);
    socket.Close();
    socket.Dispose();            

    return response;
}
```

That seems to be a pattern people use a lot even though `.Close()` doesn't do much and also calls `.Dispose()` [under the hood](https://github.com/dotnet/runtime/blob/1d1bf92fcf43aa6981804dc53c5174445069c9e4/src/libraries/System.Net.Sockets/src/System/Net/Sockets/Socket.cs#L942).

![Version 3 disconnected](images/version3_disconnected.jpg)
Note: The error states aren't errored because in version 2 they were changed to regular logging calls.

With that knowledge we can continue with using declaration in the first code example above. This also means that every socket will be properly cleaned up (assuming there's no message in-flight) with whatever way a user closes mGBA-http.