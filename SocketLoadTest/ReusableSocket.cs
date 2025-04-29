using Microsoft.Extensions.ObjectPool;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SocketLoadTestServer
{
    public class ReusableSocket : IResettable, IDisposable
    {
        private readonly Socket _socket;
        private readonly IPEndPoint _ipEndpoint;

        public ReusableSocket(string ipAddress, int port)
        {

            var address = IPAddress.Parse("127.0.0.1");
            _ipEndpoint = new IPEndPoint(address, port);
            _socket = new Socket(_ipEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        }

        public async Task<string> SendMessageAsync(string message)
        {
            if (!_socket.Connected)
            {
                await _socket.ConnectAsync(_ipEndpoint);
            }

            var messageBytes = Encoding.UTF8.GetBytes(message);
            await _socket.SendAsync(messageBytes, SocketFlags.None);

            var buffer = new byte[1_024];
            var received = await _socket.ReceiveAsync(buffer, SocketFlags.None);
            var response = Encoding.UTF8.GetString(buffer, 0, received);

            return response;
        }

        public bool TryReset()
        {
            return true;
        }

        public void Dispose()
        {
            _socket?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
