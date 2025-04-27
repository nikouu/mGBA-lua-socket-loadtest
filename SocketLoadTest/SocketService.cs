using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SocketLoadTestServer
{
    public class SocketService: IDisposable
    {
        private readonly IPEndPoint _ipEndpoint;
        private readonly Socket _socket;

        public SocketService()
        {
            var ipAddress = IPAddress.Parse("127.0.0.1");
            _ipEndpoint = new(ipAddress, 8888);
            _socket = new(_ipEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
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

        protected virtual void Dispose(bool disposing)
        {
            Console.Beep();
            if (disposing)
            {
                if (_socket.Connected)
                {
                    _socket.Shutdown(SocketShutdown.Both);
                    _socket.Close();
                }
            }

            _socket.Dispose();
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
