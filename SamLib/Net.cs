namespace SamLib.Net
{
    #region Imports
    using Open.Nat;
    using System.Collections.Concurrent;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Channels;
    #endregion

    public static class Helper
    {
        public static async Task SendFramedPayload(NetworkStream stream, byte[] data)
        {
            byte[] lengthPrefix = BitConverter.GetBytes(data.Length);

            await stream.WriteAsync(lengthPrefix, 0, lengthPrefix.Length);
            await stream.WriteAsync(data, 0, data.Length);
        }

        public static async Task<byte[]> ReadFramedPayload(NetworkStream stream)
        {
            try
            {
                byte[] lengthBuffer = new byte[4];
                await stream.ReadExactlyAsync(lengthBuffer, 0, 4);

                int dataLength = BitConverter.ToInt32(lengthBuffer, 0);

                if (dataLength > 10 * 1024 * 1024) return null;

                byte[] dataBuffer = new byte[dataLength];
                await stream.ReadExactlyAsync(dataBuffer, 0, dataLength);
                return dataBuffer;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get public IPv4
        /// </summary>
        /// <returns>Returns public IPv4</returns>
        public static async Task<string> GetIPv4()
        {
            using var client = new HttpClient();
            string publicIp = await client.GetStringAsync("https://api.ipify.org");
            return publicIp.Trim();
        }

        public static async Task<bool> PortForward(int port, Protocol protocol, int delay = 2000, string description = "SamLib Server", bool doDebug = true)
        {
            try
            {
                var discoverer = new NatDiscoverer();
                var cts = new CancellationTokenSource(5000);
                var device = await discoverer.DiscoverDeviceAsync(PortMapper.Upnp | PortMapper.Pmp, cts);

                try
                {
                    await device.DeletePortMapAsync(new Mapping(protocol, port, port, description));
                    if (doDebug) Console.WriteLine("[NAT] Sent Delete request...");
                }
                catch { }

                await Task.Delay(delay);

                string uniqueDesc = $"{description} {DateTime.Now.Ticks}";
                await device.CreatePortMapAsync(new Mapping(protocol, port, port, uniqueDesc));
                if (doDebug) Console.WriteLine($"[NAT] Port {port} forwarded successfully.");
                return true;
            }
            catch (Exception ex)
            {
                if (doDebug) Console.WriteLine($"[NAT Error] {ex.Message}");
                return false;
            }
        }

    }

    #region TCP
    public class NetServer
    {
        private class ConnectedClient
        {
            public TcpClient Client { get; set; }
            public Channel<byte[]> MessageQueue { get; set; }
        }

        public int _port;
        private TcpListener _listener;
        private int _maxClients;
        public bool _started = false;
        public bool _doDebug = false;

        private TaskCompletionSource<bool> _startTcs = new TaskCompletionSource<bool>();
        private readonly ConcurrentDictionary<string, ConnectedClient> _clients = new();
        private readonly Channel<(string clientId, byte[] data)> _msgChannel = Channel.CreateUnbounded<(string, byte[])>();
        private readonly Channel<string> _connectionChannel = Channel.CreateUnbounded<string>();

        // Assignable Action: (ClientId, Data)
        public Action<string, byte[]> OnMessageReceived { get; set; }

        // Assignable Action for Connection/Disconnection
        public Action<string> OnClientConnected { get; set; }
        public Action<string> OnClientDisconnected { get; set; }

        // Constructor
        public NetServer(int port = 8080, bool doDebug = true, int maxClients = 1)
        {
            this._port = port;
            this._maxClients = maxClients;
            this._doDebug = doDebug;
        }

        /// <summary>
        /// Awaitable to wait for server to start
        /// </summary>
        /// <returns></returns>
        public async Task WaitForStart()
        {
            if (_started) return;
            await _startTcs.Task;
        }

        /// <summary>
        /// Get server ip + port
        /// </summary>
        /// <returns></returns>
        public async Task<string> GetServerAddress() => $"{await Helper.GetIPv4()}:{_port}";

        /// <summary>
        /// Starts the server
        /// </summary>
        /// <param name="description">Description</param>
        /// <param name="maxAttempts">Max trys of different ports</param>
        /// <returns></returns>
        public async Task StartServer(string description = "server", int maxAttempts = 5, int delay = 2000, bool doPortForward = true)
        {
            int attempt = 0;
            bool success = false;
            while ((!success && (attempt < maxAttempts)) && doPortForward)
            {
                success = await Helper.PortForward(_port, Protocol.Tcp, delay, description);
                if (success) break;
                attempt++;
                _port++;
            }

            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listener.Start();

            if (_doDebug) Console.WriteLine($"Server started on {_port}.");

            // Signal started
            _started = true;
            _startTcs.TrySetResult(true);

            // Server loop
            while (true)
            {
                // Accept clients
                var client = await _listener.AcceptTcpClientAsync();

                if (_clients.Count >= _maxClients)
                {
                    if (_doDebug) Console.WriteLine("Server full. Rejecting connection.");
                    client.Close();
                    continue;
                }

                var clientInfo = new ConnectedClient
                {
                    Client = client,
                    MessageQueue = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(100)
                    {
                        FullMode = BoundedChannelFullMode.DropOldest,
                        SingleReader = false,
                        SingleWriter = true
                    })
                };

                string clientId = client.Client.RemoteEndPoint.ToString();

                if (_clients.TryAdd(clientId, clientInfo))
                {
                    OnClientConnected?.Invoke(clientId);
                    _connectionChannel.Writer.TryWrite(clientId);
                    if (_doDebug) Console.WriteLine($"Client connected: {clientId}. Total: {_clients.Count}");
                    _ = HandleClientAsync(clientId, clientInfo);
                }
            }
        }

        private async Task HandleClientAsync(string clientId, ConnectedClient clientInfo)
        {
            try
            {
                using var stream = clientInfo.Client.GetStream();

                while (true)
                {
                    byte[] receivedData = await Helper.ReadFramedPayload(stream);
                    if (receivedData == null) break; // Diconnected

                    NotifyMessageReceived(clientId, receivedData);

                    await clientInfo.MessageQueue.Writer.WriteAsync(receivedData);
                }
            }
            catch (Exception ex)
            {
                if (_doDebug) Console.WriteLine($"Error with client {clientId}: {ex.Message}");
            }
            finally
            {
                if (_clients.TryRemove(clientId, out var removedClient))
                {
                    removedClient.MessageQueue.Writer.TryComplete();

                    removedClient.Client.Close();
                    removedClient.Client.Dispose();

                    removedClient = null;
                }

                if (_doDebug) Console.WriteLine($"Client disconnected: {clientId}.");
                OnClientDisconnected?.Invoke(clientId);
            }
        }

        /// <summary>
        /// Send a message to every client
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task Broadcast(string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            foreach (var clientInfo in _clients.Values)
            {
                try
                {
                    await Helper.SendFramedPayload(clientInfo.Client.GetStream(), data);
                }
                catch { /* Failed delivery to specific client */ }
            }
        }

        /// <summary>
        /// Sends a message to a specific client
        /// </summary>
        /// <param name="clientId">IP</param>
        /// <param name="message">message</param>
        /// <returns>true/false</returns>
        public async Task<bool> SendToClient(string clientId, string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            return await SendToClient(clientId, data);
        }

        /// <summary>
        /// Sends a message to a specific client
        /// </summary>
        /// <param name="clientId">IP</param>
        /// <param name="data">data bytes</param>
        /// <returns>true/false</returns>
        public async Task<bool> SendToClient(string clientId, byte[] data)
        {
            if (_clients.TryGetValue(clientId, out var clientInfo))
            {
                try
                {
                    await Helper.SendFramedPayload(clientInfo.Client.GetStream(), data);
                    return true;
                }
                catch
                {
                    // If writing fails, the client likely disconnected
                    return false;
                }
            }
            return false; // Client ID not found
        }

        /// <summary>
        /// Blocks (asynchronously) until a client connects.
        /// </summary>
        /// <returns>The ClientId of the newly connected client.</returns>
        public async Task<string> WaitForClientToConnectAsync(CancellationToken ct = default)
        {
            return await _connectionChannel.Reader.ReadAsync(ct);
        }

        private void NotifyMessageReceived(string clientId, byte[] data)
        {
            _msgChannel.Writer.TryWrite((clientId, data));
            OnMessageReceived?.Invoke(clientId, data);
        }

        /// <summary>
        /// Waits for the next message from any client
        /// </summary>
        /// <returns>clientId & data sent</returns>
        public async Task<(string clientId, byte[] data)> WaitForNextMessageAsyncFull()
        {
            return await _msgChannel.Reader.ReadAsync();
        }

        /// <summary>
        /// Waits for the next message from any client
        /// </summary>
        /// <returns>Just string</returns>
        public async Task<string> WaitForNextMessageAsync()
        {
            var (client, data) = await _msgChannel.Reader.ReadAsync();
            return Encoding.UTF8.GetString(data);
        }

        /// <summary>
        /// Wait for next message from a specific client
        /// </summary>
        /// <param name="targetClientId">Client ID</param>
        /// <returns>Bytes send by that client</returns>
        public async Task<string> WaitForMessageFromClientAsync(string targetClientId)
        {
            if (_clients.TryGetValue(targetClientId, out var clientInfo))
            {
                byte[] data = await clientInfo.MessageQueue.Reader.ReadAsync();
                return Encoding.UTF8.GetString(data);
            }
            throw new Exception("Client not found.");
        }

        /// <summary>
        /// Displays all connected clients
        /// </summary>
        /// <returns></returns>
        public async Task ListClient()
        {
            foreach (KeyValuePair<string, ConnectedClient> pair in _clients)
            {
                Console.WriteLine(pair.Key);
            }
        }

        /// <summary>
        /// Returns all clients as a list
        /// </summary>
        /// <returns>List of clients</returns>
        public async Task<List<string>> GetAllClients()
        {
            return _clients.Keys.ToList();
        }
    }

    public class NetClient
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private readonly string _hostname;
        private readonly int _port;
        private bool _isConnected;
        private bool _doDebug = false;

        private readonly Channel<byte[]> _msgChannel = Channel.CreateUnbounded<byte[]>();

        // Assignable Actions (Callbacks)
        public Action<byte[]> OnMessageReceived { get; set; }
        public Action OnConnected { get; set; }
        public Action OnDisconnected { get; set; }

        public NetClient(string hostname = "127.0.0.1", int port = 8080, bool doDebug = false)
        {
            _hostname = hostname;
            _port = port;
            _doDebug = doDebug;
        }

        /// <summary>
        /// Connects to the given server
        /// </summary>
        /// <returns>true/false</returns>
        public async Task<bool> Connect(bool allowRetry = false, int maxRetry = 1)
        {
            int attempts = 0;
            int limit = allowRetry ? maxRetry : 1;

            while (attempts < limit)
            {
                try
                {
                    _isConnected = false;
                    _client?.Dispose();

                    _client = new TcpClient();
                    await _client.ConnectAsync(_hostname, _port);
                    _stream = _client.GetStream();
                    _isConnected = true;

                    OnConnected?.Invoke();
                    _ = ReceiveLoop();
                    return true;
                }
                catch
                {
                    attempts++;
                    if (_doDebug) Console.WriteLine($"Connection failed: Retrying {attempts + 1}/{limit}");
                    if (attempts < limit) await Task.Delay(1000);
                }
            }
            return false;
        }

        private void NotifyMessageReceived(byte[] data)
        {
            _msgChannel.Writer.TryWrite(data);
            OnMessageReceived?.Invoke(data);
        }

        /// <summary>
        /// Wait for the next message to be recieved
        /// </summary>
        /// <returns>string</returns>
        public async Task<string> WaitForNextMessageAsync()
        {
            byte[] data = await _msgChannel.Reader.ReadAsync();
            string msg = Encoding.UTF8.GetString(data);
            return msg;
        }

        /// <summary>
        /// Wait for the next message to be recieved
        /// </summary>
        /// <returns>bytes</returns>
        public async Task<byte[]> WaitForNextMessageAsyncFull()
        {
            return await _msgChannel.Reader.ReadAsync();
        }

        private async Task ReceiveLoop()
        {
            try
            {
                while (_client != null && _client.Connected)
                {
                    byte[] data = await Helper.ReadFramedPayload(_stream);

                    if (data == null) break;

                    NotifyMessageReceived(data);
                }
            }
            catch
            {
                // Likely a connection drop
            }
            finally
            {
                Disconnect();
            }
        }

        /// <summary>
        /// Sends a message to the server
        /// </summary>
        /// <param name="message">message</param>
        /// <returns></returns>
        public async Task Send(string message)
        {
            if (!_isConnected || _stream == null) return;
            byte[] data = Encoding.UTF8.GetBytes(message);
            await Send(data);
        }

        /// <summary>
        /// Sends a byte array to the server
        /// </summary>
        /// <param name="data">bytes</param>
        /// <returns></returns>
        public async Task Send(byte[] data)
        {
            if (!_isConnected || _stream == null) return;
            try
            {
                await Helper.SendFramedPayload(_stream, data);
            }
            catch { Disconnect(); }
        }

        /// <summary>
        /// Disconnects from the server
        /// </summary>
        public void Disconnect()
        {
            if (!_isConnected) return;

            _isConnected = false;
            _stream?.Dispose();
            _client?.Dispose();

            OnDisconnected?.Invoke();
            if (_doDebug) Console.WriteLine("Disconnected from server.");
        }
    }
    #endregion
}
