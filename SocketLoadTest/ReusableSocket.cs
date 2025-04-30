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
        private const int _maxRetries = 3;
        private const int _initialDelay = 400;
        private const int _maxDelay = 2000;

        public ReusableSocket(string ipAddress, int port)
        {

            var address = IPAddress.Parse("127.0.0.1");
            _ipEndpoint = new IPEndPoint(address, port);
            _socket = new Socket(_ipEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        }

        public async Task<string> SendMessageAsync(string message)
        {
            var attempts = 0;
            var delay = _initialDelay;

            while (attempts < _maxRetries)
            {
                try
                {
                    attempts++;
                    return await SendAsync(message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{attempts}][{message}]{ex.Message}");
                    if (attempts >= _maxRetries)
                    {
                        throw;
                    }

                    await Task.Delay(delay);
                    delay = Math.Min(delay * 3, _maxDelay);
                }
            }

            throw new Exception("How did we get here?");
        }

        private async Task<string> SendAsync(string message)
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
