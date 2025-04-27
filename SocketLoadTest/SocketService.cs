using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SocketLoadTestServer
{
    public class SocketService
    {
        public async Task<string> SendMessageAsync(string message)
        {
            var ipAddress = IPAddress.Parse("127.0.0.1");
            var ipEndpoint = new IPEndPoint(ipAddress, 8888);
            using var socket = new Socket(ipEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            await socket.ConnectAsync(ipEndpoint);

            var messageBytes = Encoding.UTF8.GetBytes(message);
            await socket.SendAsync(messageBytes, SocketFlags.None);

            var buffer = new byte[1_024];
            var received = await socket.ReceiveAsync(buffer, SocketFlags.None);
            var response = Encoding.UTF8.GetString(buffer, 0, received);

            return response;
        }
    }
}
