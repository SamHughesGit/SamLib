namespace SamLib.Net
{
    #region Imports
    using Open.Nat;
    using System.Collections.Concurrent;
    using System.Net;
    using System.Net.Sockets;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Channels;
    #endregion

    public static class Helper
    {

        public static class Crypto
        {
            // Generate a new RSA Key Pair
            public static (string publicK, string privateK) GenerateRSAKeys()
            {
                using var rsa = new RSACryptoServiceProvider(2048);
                return (rsa.ToXmlString(false), rsa.ToXmlString(true));
            }

            // Encrypt with Public Key (Client side)
            public static byte[] EncryptRSA(string publicKeyXml, byte[] data)
            {
                if (string.IsNullOrEmpty(publicKeyXml) || !publicKeyXml.StartsWith("<RSAKeyValue>"))
                {
                    throw new ArgumentException("Invalid RSA Public Key format. Expected XML string.");
                }

                using var rsa = new RSACryptoServiceProvider();
                rsa.FromXmlString(publicKeyXml);
                return rsa.Encrypt(data, fOAEP: true);
            }

            // Decrypt with Private Key (Server side)
            public static byte[] DecryptRSA(string privateKey, byte[] data)
            {
                if (data == null || data.Length == 0)
                    throw new ArgumentNullException(nameof(data), "RSA Decryption failed: Data buffer is null or empty.");

                using var rsa = new RSACryptoServiceProvider();
                rsa.FromXmlString(privateKey);
                return rsa.Decrypt(data, fOAEP: true);
            }

            // AES-GCM settings
            private const int NonceSize = 12; // Standard for GCM
            private const int TagSize = 16;   // Authentication tag size

            public static byte[] EncryptAES(byte[] data, byte[] key)
            {
                using var aes = new AesGcm(key);

                // We need a unique 'nonce' for every message. 
                byte[] nonce = new byte[NonceSize];
                RandomNumberGenerator.Fill(nonce);

                byte[] ciphertext = new byte[data.Length];
                byte[] tag = new byte[TagSize];

                aes.Encrypt(nonce, data, ciphertext, tag);

                // Combine Nonce + Tag + Ciphertext into one array to send
                byte[] result = new byte[NonceSize + TagSize + ciphertext.Length];
                Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
                Buffer.BlockCopy(tag, 0, result, NonceSize, TagSize);
                Buffer.BlockCopy(ciphertext, 0, result, NonceSize + TagSize, ciphertext.Length);

                return result;
            }

            public static byte[] DecryptAES(byte[] encryptedData, byte[] key)
            {
                if (encryptedData.Length < NonceSize + TagSize) return null;

                using var aes = new AesGcm(key);

                // Extract the pieces
                byte[] nonce = new byte[NonceSize];
                byte[] tag = new byte[TagSize];
                byte[] ciphertext = new byte[encryptedData.Length - NonceSize - TagSize];

                Buffer.BlockCopy(encryptedData, 0, nonce, 0, NonceSize);
                Buffer.BlockCopy(encryptedData, NonceSize, tag, 0, TagSize);
                Buffer.BlockCopy(encryptedData, NonceSize + TagSize, ciphertext, 0, ciphertext.Length);

                byte[] decryptedData = new byte[ciphertext.Length];

                try
                {
                    // This will throw an exception if the data was tampered with
                    aes.Decrypt(nonce, ciphertext, tag, decryptedData);
                    return decryptedData;
                }
                catch (CryptographicException)
                {
                    // Data was modified or key is wrong
                    return null;
                }
            }
        }

        public static async Task SendFramedPayload(NetworkStream stream, byte[] data)
        {
            byte[] lengthPrefix = BitConverter.GetBytes(data.Length);

            await stream.WriteAsync(lengthPrefix, 0, lengthPrefix.Length);
            await stream.WriteAsync(data, 0, data.Length);
        }

        public static async Task<byte[]> ReadFramedPayload(NetworkStream stream)
        {
            // Read the Int32 length prefix (4 bytes)
            byte[] lengthBuffer = new byte[4];
            int bytesRead = 0;
            while (bytesRead < 4)
            {
                int read = await stream.ReadAsync(lengthBuffer, bytesRead, 4 - bytesRead);
                if (read == 0) return null; // Connection closed
                bytesRead += read;
            }
            int payloadLength = BitConverter.ToInt32(lengthBuffer, 0);

            // Read the actual payload based on that length
            byte[] payload = new byte[payloadLength];
            int totalReceived = 0;
            while (totalReceived < payloadLength)
            {
                int read = await stream.ReadAsync(payload, totalReceived, payloadLength - totalReceived);
                if (read == 0) return null; // Connection closed mid-payload
                totalReceived += read;
            }

            return payload;
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

        // Generate unique token for linking Tcp and Udp connections
        private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!£$%^&*()-=+_[]{}<>,.?";
        public static async Task<string> GenerateToken(int length = 32)
        {
            return string.Create(length, Chars, (span, alphabet) =>
            {
                Random rng = new Random();
                for (int i = 0; i < span.Length; i++)
                {
                    span[i] = alphabet[rng.Next(0, alphabet.Length)];
                }
            });
        }

        /*
         * For multiplayer linking TCP and UDP:
         * - Generate a token on the server side once a tcp client connects and link it to that players object
         * - Send the token to the TCP client
         * - The TCP client also runs a UDP client
         * - This UDP client connects to the servers UDP server
         * - Every x ms send the token over UDP until:
         * - The server recieves it and verifies that it is a real token belonging to one of the objects
         * - The server confirms with the TCP client that it has recieved it so it can stop sending it
         * - That UDP endpoint is linked with the player object, then messages from that UDP client can be used to update the player
         */
    }

    #region TCP
    public class TcpNetServer
    {
        private class ConnectedClient
        {
            public TcpClient Client { get; set; }
            public Channel<byte[]> MessageQueue { get; set; }
            public byte[] AesSessionKey { get; set; }
            public bool IsSecure { get; set; } = false;
        }

        public int _port;
        private TcpListener _listener;
        private int _maxClients;
        public bool _started = false;
        public bool _doDebug = false;

        private TaskCompletionSource<bool> _startTcs = new TaskCompletionSource<bool>();
        private readonly ConcurrentDictionary<string, ConnectedClient> _clients = new ConcurrentDictionary<string, ConnectedClient>();
        private readonly Channel<(string clientId, byte[] data)> _msgChannel = Channel.CreateUnbounded<(string, byte[])>();
        private readonly Channel<string> _connectionChannel = Channel.CreateUnbounded<string>();

        // Encryption
        private bool _doEncrypt = false;
        private string _privKey;
        private string _pubKey;

        // Assignable Action: (ClientId, Data)
        public Action<string, byte[]> OnMessageReceived { get; set; }

        // Assignable Action for Connection/Disconnection
        public Action<string> OnClientConnected { get; set; }
        public Action<string> OnClientDisconnected { get; set; }

        // Constructor
        public TcpNetServer(int port = 8080, bool doEncrypt = false, bool doDebug = true, int maxClients = 1)
        {
            this._port = port;
            this._maxClients = maxClients;
            this._doDebug = doDebug;
            this._doEncrypt = doEncrypt;

            // If encrypted, generate server keys
            if (_doEncrypt)
            {
                (this._pubKey, this._privKey) = Helper.Crypto.GenerateRSAKeys();
            }
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

                if (_doEncrypt)
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                    // Send Public Key
                    byte[] pubKeyBytes = Encoding.UTF8.GetBytes("PUBKEY:" + _pubKey);
                    await Helper.SendFramedPayload(stream, pubKeyBytes);

                    // Read Response
                    byte[] encryptedKeyResponse = await Helper.ReadFramedPayload(stream);
                    if (encryptedKeyResponse == null) throw new Exception("Client disconnected during handshake.");

                    string responseStr = Encoding.UTF8.GetString(encryptedKeyResponse);

                    if (responseStr.StartsWith("AESKEY:"))
                    {
                        // Safety check the substring
                        string base64Data = responseStr.Substring(7);
                        if (string.IsNullOrWhiteSpace(base64Data)) throw new Exception("Received empty AES key.");

                        byte[] encryptedAesKey = Convert.FromBase64String(base64Data);

                        // Decrypt the key
                        clientInfo.AesSessionKey = Helper.Crypto.DecryptRSA(_privKey, encryptedAesKey);
                        clientInfo.IsSecure = true;

                        if (_doDebug) Console.WriteLine($"[Security] Secure session established for {clientId}");
                    }
                    else
                    {
                        throw new Exception("Client sent invalid handshake response.");
                    }
                }

                OnClientConnected?.Invoke(clientId);
                _connectionChannel.Writer.TryWrite(clientId);

                while (true)
                {
                    byte[] receivedData = await Helper.ReadFramedPayload(stream);
                    if (receivedData == null) break; // Diconnected

                    // Decrypt the data if encryption is enabled
                    if (_doEncrypt && clientInfo.IsSecure)
                    {
                        receivedData = Helper.Crypto.DecryptAES(receivedData, clientInfo.AesSessionKey);

                        if (receivedData == null)
                        {
                            if (_doDebug) Console.WriteLine($"[Security] Decryption failed for {clientId}. Dropping packet.");
                            continue;
                        }
                    }

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
            foreach (var clientId in _clients.Keys)
            {
                await SendToClient(clientId, data);
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
                    byte[] dataToSend = data;

                    // Apply AES encryption if the handshake is complete
                    if (_doEncrypt && clientInfo.IsSecure && clientInfo.AesSessionKey != null)
                    {
                        dataToSend = Helper.Crypto.EncryptAES(data, clientInfo.AesSessionKey);
                    }

                    await Helper.SendFramedPayload(clientInfo.Client.GetStream(), dataToSend);
                    return true;
                }
                catch (Exception ex)
                {
                    if (_doDebug) Console.WriteLine($"[TCP] Send failed to {clientId}: {ex.Message}");
                    return false;
                }
            }
            return false;
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

    public class TcpNetClient
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private readonly string _hostname;
        private readonly int _port;
        private bool _isConnected;
        private bool _doDebug = false;

        // Encryption Fields
        private bool _doEncrypt;
        private byte[] _aesSessionKey;
        private bool _isSecure = false;

        private readonly Channel<byte[]> _msgChannel = Channel.CreateUnbounded<byte[]>();

        public Action<byte[]> OnMessageReceived { get; set; }
        public Action OnConnected { get; set; }
        public Action OnDisconnected { get; set; }

        public TcpNetClient(string hostname = "127.0.0.1", int port = 8080, bool doEncrypt = false, bool doDebug = false)
        {
            _hostname = hostname;
            _port = port;
            _doEncrypt = doEncrypt;
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
                    _isSecure = false;
                    _client?.Dispose();

                    _client = new TcpClient();
                    await _client.ConnectAsync(_hostname, _port);
                    _stream = _client.GetStream();

                    if (_doEncrypt)
                    {
                        if (!await PerformHandshake()) return false;
                    }

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

        private async Task<bool> PerformHandshake()
        {
            try
            {
                // Wait for Server's Public Key
                byte[] keyData = await Helper.ReadFramedPayload(_stream);
                string keyMsg = Encoding.UTF8.GetString(keyData);

                if (!keyMsg.StartsWith("PUBKEY:")) throw new Exception("Invalid Handshake");
                string pubKey = keyMsg.Substring(7);

                // Generate a random 32-byte AES Key
                _aesSessionKey = new byte[32];
                using (var rng = RandomNumberGenerator.Create()) { rng.GetBytes(_aesSessionKey); }

                // Encrypt AES Key with Server's Public RSA Key
                byte[] encryptedAesKey = Helper.Crypto.EncryptRSA(pubKey, _aesSessionKey);
                string response = "AESKEY:" + Convert.ToBase64String(encryptedAesKey);

                // Send back to server
                await Helper.SendFramedPayload(_stream, Encoding.UTF8.GetBytes(response));

                _isSecure = true;
                if (_doDebug) Console.WriteLine("[Security] Encrypted session established.");
                return true;
            }
            catch (Exception ex)
            {
                if (_doDebug) Console.WriteLine($"[Security] Handshake failed: {ex.Message}");
                return false;
            }
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

                    // DECRYPT
                    if (_doEncrypt && _isSecure)
                    {
                        data = Helper.Crypto.DecryptAES(data, _aesSessionKey);
                        if (data == null) continue; // Failed decryption
                    }

                    NotifyMessageReceived(data);
                }
            }
            catch { /* Connection dropped */ }
            finally { Disconnect(); }
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
                byte[] dataToSend = data;

                // ENCRYPT
                if (_doEncrypt && _isSecure)
                {
                    dataToSend = Helper.Crypto.EncryptAES(data, _aesSessionKey);
                }

                await Helper.SendFramedPayload(_stream, dataToSend);
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

    #region UDP
    public class UdpNetServer
    {
        private class ConnectedClient
        {
            public IPEndPoint EndPoint { get; set; }
            public Channel<byte[]> MessageQueue { get; set; }
            public DateTime LastHeartbeat { get; set; } 
        }

        public int _port;
        private UdpClient _udpListener;
        private int _maxClients;
        public bool _started = false;
        public bool _doDebug = false;
        public bool _autoDisconnect = true;
        public int _timeout = 10;

        private TaskCompletionSource<bool> _startTcs = new TaskCompletionSource<bool>();
        private readonly ConcurrentDictionary<string, ConnectedClient> _clients = new();
        private readonly Channel<(string clientId, byte[] data)> _msgChannel = Channel.CreateUnbounded<(string, byte[])>();
        private readonly Channel<string> _connectionChannel = Channel.CreateUnbounded<string>();

        public Action<string, byte[]> OnMessageReceived { get; set; }
        public Action<string> OnClientConnected { get; set; }
        public Action<string> OnClientDisconnected { get; set; }

        public UdpNetServer(int port = 8080, bool doDebug = true, bool autoDisconnect = true, int maxClients = 1, int timeOut = 10)
        {
            this._port = port;
            this._maxClients = maxClients;
            this._doDebug = doDebug;
            this._autoDisconnect = autoDisconnect;
            this._timeout = timeOut;
        }

        public async Task WaitForStart()
        {
            if (_started) return;
            await _startTcs.Task;
        }

        public async Task<string> GetServerAddress() => $"{await Helper.GetIPv4()}:{_port}";

        public async Task StartServer(string description = "udp_server", int maxAttempts = 5, int delay = 2000, bool doPortForward = true)
        {
            int attempt = 0;
            bool success = false;

            // Try port forwarding 
            while ((!success && (attempt < maxAttempts)) && doPortForward)
            {
                success = await Helper.PortForward(_port, Protocol.Udp, delay, description);
                if (success) break;
                attempt++;
                _port++;
            }

            try
            {
                _udpListener = new UdpClient(_port);

                // Fix for Windows UDP socket error 10054 (ConnectionReset) when a client closes unexpectedly
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    _udpListener.Client.IOControl((IOControlCode)(-1744830452), new byte[] { 0, 0, 0, 0 }, null);
                }
            }
            catch (Exception ex)
            {
                if (_doDebug) Console.WriteLine($"Failed to bind UDP: {ex.Message}");
                return;
            }

            if (_doDebug) Console.WriteLine($"UDP Server started on {_port}.");

            _started = true;
            _startTcs.TrySetResult(true);

            // Start the receive loop
            _ = ReceiveLoop();
            _ = ClientCleanupLoop();
        }

        private async Task ReceiveLoop()
        {
            while (true)
            {
                try
                {
                    var result = await _udpListener.ReceiveAsync();
                    byte[] data = result.Buffer;
                    IPEndPoint remoteEndPoint = result.RemoteEndPoint;
                    string clientId = remoteEndPoint.ToString();

                    // Check if existing client
                    if (!_clients.TryGetValue(clientId, out var clientInfo))
                    {
                        // New Client
                        if (_clients.Count >= _maxClients)
                        {
                            if (_doDebug) Console.WriteLine($"Server full. Ignoring packet from {clientId}.");
                            continue;
                        }

                        clientInfo = new ConnectedClient
                        {
                            EndPoint = remoteEndPoint,
                            MessageQueue = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(100)
                            {
                                FullMode = BoundedChannelFullMode.DropOldest,
                                SingleReader = false,
                                SingleWriter = true
                            })
                        };

                        if (_clients.TryAdd(clientId, clientInfo))
                        {
                            OnClientConnected?.Invoke(clientId);
                            _connectionChannel.Writer.TryWrite(clientId);
                            if (_doDebug) Console.WriteLine($"Client connected (UDP): {clientId}. Total: {_clients.Count}");
                        }
                    }

                    // Process Data
                    if (data.Length > 0)
                    {
                        NotifyMessageReceived(clientId, data);
                        await clientInfo.MessageQueue.Writer.WriteAsync(data);
                    }

                    clientInfo.LastHeartbeat = DateTime.UtcNow;
                }
                catch (ObjectDisposedException)
                {
                    break; // Server stopped
                }
                catch (Exception ex)
                {
                    if (_doDebug) Console.WriteLine($"UDP Receive Error: {ex.Message}");
                }
            }
        }

        private async Task ClientCleanupLoop()
        {
            while (_started)
            {
                await Task.Delay(5000);
                if (_autoDisconnect)
                {
                    var now = DateTime.UtcNow;
                    var timeout = TimeSpan.FromSeconds(_timeout);

                    foreach (var client in _clients)
                    {
                        if (now - client.Value.LastHeartbeat > timeout)
                        {
                            if (_clients.TryRemove(client.Key, out var removed))
                            {
                                if (_doDebug) Console.WriteLine($"Client timed out: {client.Key}");
                                OnClientDisconnected?.Invoke(client.Key);
                                removed.MessageQueue.Writer.TryComplete();
                            }
                        }
                    }
                }
            }
        }

        public async Task Broadcast(string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            foreach (var clientInfo in _clients.Values)
            {
                await _udpListener.SendAsync(data, data.Length, clientInfo.EndPoint);
            }
        }

        public async Task<bool> SendToClient(string clientId, string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            return await SendToClient(clientId, data);
        }

        public async Task<bool> SendToClient(string clientId, byte[] data)
        {
            if (_clients.TryGetValue(clientId, out var clientInfo))
            {
                try
                {
                    await _udpListener.SendAsync(data, data.Length, clientInfo.EndPoint);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }

        public async Task<string> WaitForClientToConnectAsync(CancellationToken ct = default)
        {
            return await _connectionChannel.Reader.ReadAsync(ct);
        }

        private void NotifyMessageReceived(string clientId, byte[] data)
        {
            _msgChannel.Writer.TryWrite((clientId, data));
            OnMessageReceived?.Invoke(clientId, data);
        }

        public async Task<(string clientId, byte[] data)> WaitForNextMessageAsyncFull()
        {
            return await _msgChannel.Reader.ReadAsync();
        }

        public async Task<string> WaitForNextMessageAsync()
        {
            var (client, data) = await _msgChannel.Reader.ReadAsync();
            return Encoding.UTF8.GetString(data);
        }

        public async Task<string> WaitForMessageFromClientAsync(string targetClientId)
        {
            if (_clients.TryGetValue(targetClientId, out var clientInfo))
            {
                byte[] data = await clientInfo.MessageQueue.Reader.ReadAsync();
                return Encoding.UTF8.GetString(data);
            }
            throw new Exception("Client not found.");
        }

        public async Task ListClient()
        {
            foreach (var key in _clients.Keys)
            {
                Console.WriteLine(key);
            }
        }

        public async Task<List<string>> GetAllClients()
        {
            return _clients.Keys.ToList();
        }
    }

    public class UdpNetClient
    {
        private UdpClient _client;
        private readonly string _hostname;
        private readonly int _port;
        private bool _isConnected;
        private bool _doDebug = false;

        private readonly Channel<byte[]> _msgChannel = Channel.CreateUnbounded<byte[]>();

        public Action<byte[]> OnMessageReceived { get; set; }
        public Action OnConnected { get; set; }
        public Action OnDisconnected { get; set; }

        public UdpNetClient(string hostname = "127.0.0.1", int port = 8080, bool doDebug = false)
        {
            _hostname = hostname;
            _port = port;
            _doDebug = doDebug;
        }

        public async Task<bool> Connect(bool allowRetry = false, int maxRetry = 1)
        {
            try
            {
                _client?.Dispose();
                _client = new UdpClient();

                // Fix for Windows UDP socket error 10054
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    _client.Client.IOControl((IOControlCode)(-1744830452), new byte[] { 0, 0, 0, 0 }, null);
                }

                _client.Connect(_hostname, _port);

                _isConnected = true;
                OnConnected?.Invoke();
                _ = ReceiveLoop();

                byte[] handshake = Encoding.UTF8.GetBytes("HELLO_SERVER");
                await Send(handshake);

                return true;
            }
            catch (Exception ex)
            {
                if (_doDebug) Console.WriteLine($"UDP Connect Failed: {ex.Message}");
                return false;
            }
        }

        private void NotifyMessageReceived(byte[] data)
        {
            _msgChannel.Writer.TryWrite(data);
            OnMessageReceived?.Invoke(data);
        }

        public async Task<string> WaitForNextMessageAsync()
        {
            byte[] data = await _msgChannel.Reader.ReadAsync();
            return Encoding.UTF8.GetString(data);
        }

        public async Task<byte[]> WaitForNextMessageAsyncFull()
        {
            return await _msgChannel.Reader.ReadAsync();
        }

        private async Task ReceiveLoop()
        {
            try
            {
                while (_isConnected)
                {
                    // ReceiveAsync waits for a packet from the "Connected" remote host
                    var result = await _client.ReceiveAsync();
                    byte[] data = result.Buffer;

                    if (data != null && data.Length > 0)
                    {
                        NotifyMessageReceived(data);
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // Client closed
            }
            catch (Exception ex)
            {
                if (_doDebug) Console.WriteLine($"UDP Receive Error: {ex.Message}");
            }
            finally
            {
                Disconnect();
            }
        }

        public async Task Send(string message)
        {
            if (!_isConnected) return;
            byte[] data = Encoding.UTF8.GetBytes(message);
            await Send(data);
        }

        public async Task Send(byte[] data)
        {
            if (!_isConnected) return;
            try
            {
                await _client.SendAsync(data, data.Length);
            }
            catch
            {
                Disconnect();
            }
        }

        public void Disconnect()
        {
            if (!_isConnected) return;

            _isConnected = false;
            _client?.Close();
            _client?.Dispose();

            OnDisconnected?.Invoke();
            if (_doDebug) Console.WriteLine("Disconnected from server.");
        }
    }
    #endregion

    #region Linker
    public class NetLinker
    {
        private readonly TcpNetServer _tcp;
        private readonly UdpNetServer _udp;
        private bool _debug;

        // Maps Token -> TCP ClientID
        private readonly ConcurrentDictionary<string, string> _pendingTokens = new();
        // Maps TCP ClientID -> UDP EndPoint str
        private readonly ConcurrentDictionary<string, string> _links = new();

        public NetLinker(TcpNetServer tcp, UdpNetServer udp, bool debug = false)
        {
            _tcp = tcp;
            _udp = udp;
            _debug = debug;

            // Listen for the UDP handshake packets
            _udp.OnMessageReceived += async (udpId, data) =>
            {
                string msg = Encoding.UTF8.GetString(data);
                if (msg.StartsWith("AUTH:"))
                {
                    string token = msg.Substring(5);
                    if (_pendingTokens.TryRemove(token, out string tcpId))
                    {
                        _links[tcpId] = udpId;
                        // Send OK via TCP
                        await _tcp.SendToClient(tcpId, "AUTH_OK");
                        if(_debug)Console.WriteLine($"[Linker] Linked TCP {tcpId} to UDP {udpId}");
                    }
                }
            };
        }

        public async Task StartLink(string tcpId)
        {
            string token = await Helper.GenerateToken(16);
            _pendingTokens[token] = tcpId;
            // Send token to client via TCP
            await _tcp.SendToClient(tcpId, $"TOKEN:{token}");
        }

        public string GetUdpId(string tcpId) => _links.TryGetValue(tcpId, out var id) ? id : null;
        public void Remove(string tcpId) => _links.TryRemove(tcpId, out _);
    }

    public class NetLinkerClient
    {
        private readonly TcpNetClient _tcp;
        private readonly UdpNetClient _udp;
        private bool _debug;
        private bool _isLinked = false;

        public NetLinkerClient(TcpNetClient tcp, UdpNetClient udp, bool debug)
        {
            _tcp = tcp;
            _udp = udp; 
            _debug = debug;
        }

        public void InitHandshake()
        {
            _tcp.OnMessageReceived += HandleHandshakeMessages;
        }

        private async void HandleHandshakeMessages(byte[] data)
        {
            string msg = Encoding.UTF8.GetString(data);

            if (msg.StartsWith("TOKEN:"))
            {
                string token = msg.Substring(6);
                _ = BeginUdpSpam(token);
            }
            else if (msg == "AUTH_OK")
            {
                _isLinked = true;

                // Unsub
                _tcp.OnMessageReceived -= HandleHandshakeMessages;

                if(_debug)Console.WriteLine("[Client] Handshake Complete. Unsubscribed from TCP Auth listener.");
            }
        }

        private async Task BeginUdpSpam(string token)
        {
            _isLinked = false;
            int attempts = 0;

            while (!_isLinked && attempts < 20)
            {
                await _udp.Send($"AUTH:{token}");
                await Task.Delay(200); // 5 times per second
                attempts++;
            }

            if (!_isLinked && _debug) Console.WriteLine("[Client] UDP Link Timeout.");
        }
    }
    #endregion
}
