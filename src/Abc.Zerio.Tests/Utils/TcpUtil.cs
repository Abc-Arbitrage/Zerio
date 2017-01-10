using System.Net;
using System.Net.Sockets;

namespace Abc.Zerio.Tests.Utils
{
    public static class TcpUtil
    {
        public static int GetRandomUnusedPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            var port = ((IPEndPoint)listener.LocalEndpoint).Port;

            listener.Stop();

            return port;
        }
    }
}
