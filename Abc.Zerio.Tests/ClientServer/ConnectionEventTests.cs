using System.Threading.Tasks;
using NUnit.Framework;

namespace Abc.Zerio.Tests.ClientServer
{
    public class ConnectionEventTests : ClientServerFixture
    {
        [Test]
        public void should_raise_connection_events_on_server()
        {
            var connected = new TaskCompletionSource<int>();
            var disconnected = new TaskCompletionSource<int>();
            Server.ClientConnected += clientId => connected.SetResult(clientId);
            Server.ClientDisconnected += clientId => disconnected.SetResult(clientId);

            Server.Start();
            Client.Connect(ServerEndPoint);

            Assert.That(connected.Task.Wait(500), Is.True);

            Client.Disconnect();

            Assert.That(disconnected.Task.Wait(500), Is.True);
            Assert.That(disconnected.Task.Result, Is.EqualTo(connected.Task.Result));
        }

        [Test]
        public void should_raise_connection_events_on_client()
        {
            var connected = new TaskCompletionSource<object>();
            var disconnected = new TaskCompletionSource<object>();
            Client.Connected += () => connected.SetResult(null);
            Client.Disconnected += () => disconnected.SetResult(null);

            Server.Start();
            Client.Connect(ServerEndPoint);

            Assert.That(connected.Task.Wait(200), Is.True);

            Client.Disconnect();

            Assert.That(disconnected.Task.Wait(200), Is.True);
        }
    }
}
