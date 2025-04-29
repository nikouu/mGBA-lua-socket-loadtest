using Microsoft.Extensions.ObjectPool;

namespace SocketLoadTestServer
{
    public class ReusableSocketPooledObjectPolicy : IPooledObjectPolicy<ReusableSocket>
    {
        private readonly string _ipAddress;
        private readonly int _port;
        public ReusableSocketPooledObjectPolicy(string ipAddress, int port)
        {
            _ipAddress = ipAddress;
            _port = port;
        }

        public ReusableSocket Create()
        {
            return new ReusableSocket(_ipAddress, _port);
        }

        public bool Return(ReusableSocket obj)
        {
            return true;
        }
    }
}
