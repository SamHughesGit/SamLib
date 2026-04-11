/// AI GENERATED 

namespace SamLib.Unity.Net
{
    #region Imports
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml;
#if UNITY_2018_1_OR_NEWER
    using UnityEngine;
#endif
#if SAMLIB_OPEN_NAT
    using Open.Nat;
#endif
    #endregion

    public enum Protocol
    {
        Tcp,
        Udp
    }

    public interface IPortForwarder
    {
        Task<bool> TryForwardPortAsync(int port, Protocol protocol, int delay, string description, bool doDebug);
    }

    internal static class UnityPlatform
    {
        public static bool SupportsRawSockets
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return false;
#else
                return true;
#endif
            }
        }

        public static string GetRawSocketError(string transport)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return transport + " is not available on Unity WebGL players because raw sockets are unsupported there.";
#else
            return transport + " is not available on this platform.";
#endif
        }

        public static void ThrowIfRawSocketsUnsupported(string transport)
        {
            if (!SupportsRawSockets)
            {
                throw new PlatformNotSupportedException(GetRawSocketError(transport));
            }
        }
    }

    internal static class UnityNetLog
    {
        public static void Info(bool doLog, string message)
        {
            if (!doLog)
            {
                return;
            }

#if UNITY_2018_1_OR_NEWER
            if (UnityThreadDispatcher.IsInstalled)
            {
                UnityThreadDispatcher.Invoke(delegate { Debug.Log(message); }, true, false, "Debug.Log");
            }
            else
            {
                Console.WriteLine(message);
            }
#else
            Console.WriteLine(message);
#endif
        }

        public static void Error(bool doLog, string message)
        {
            if (!doLog)
            {
                return;
            }

#if UNITY_2018_1_OR_NEWER
            if (UnityThreadDispatcher.IsInstalled)
            {
                UnityThreadDispatcher.Invoke(delegate { Debug.LogError(message); }, true, false, "Debug.LogError");
            }
            else
            {
                Console.WriteLine(message);
            }
#else
            Console.WriteLine(message);
#endif
        }
    }

    internal static class UnityThreadDispatcher
    {
        private static readonly object SyncRoot = new object();
        private static SynchronizationContext _mainThreadContext;
        private static int _mainThreadId = -1;

        public static bool IsInstalled
        {
            get { return _mainThreadContext != null; }
        }

        public static bool IsMainThread
        {
            get { return Thread.CurrentThread.ManagedThreadId == _mainThreadId; }
        }

        public static void InstallFromCurrentContext()
        {
            SynchronizationContext context = SynchronizationContext.Current;
            if (context == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                _mainThreadContext = context;
                _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            }
        }

        public static void TryInstallFromCurrentContext()
        {
            if (IsInstalled)
            {
                return;
            }

            InstallFromCurrentContext();
        }

        public static void Invoke(Action action, bool marshalToMainThread, bool doDebug, string callbackName)
        {
            if (action == null)
            {
                return;
            }

            Action safeAction = delegate
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    UnityNetLog.Error(doDebug, "[Callback] " + callbackName + " threw: " + ex.Message);
                }
            };

            if (marshalToMainThread && IsInstalled && !IsMainThread)
            {
                _mainThreadContext.Post(_ => safeAction(), null);
                return;
            }

            safeAction();
        }
    }

    internal sealed class AsyncMessageQueue<T>
    {
        private readonly ConcurrentQueue<T> _queue = new ConcurrentQueue<T>();
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);
        private readonly int _capacity;
        private volatile bool _isCompleted;

        public AsyncMessageQueue(int capacity)
        {
            _capacity = capacity < 1 ? int.MaxValue : capacity;
        }

        public AsyncMessageQueue()
            : this(int.MaxValue)
        {
        }

        public void Enqueue(T item)
        {
            if (_isCompleted)
            {
                return;
            }

            while (_queue.Count >= _capacity && _queue.TryDequeue(out _))
            {
                _signal.Wait(0);
            }

            _queue.Enqueue(item);
            _signal.Release();
        }

        public async Task<T> DequeueAsync(CancellationToken ct)
        {
            while (true)
            {
                await _signal.WaitAsync(ct).ConfigureAwait(false);

                if (_queue.TryDequeue(out T item))
                {
                    return item;
                }

                if (_isCompleted)
                {
                    throw new InvalidOperationException("Queue has been completed.");
                }
            }
        }

        public Task<T> DequeueAsync()
        {
            return DequeueAsync(CancellationToken.None);
        }

        public void Complete()
        {
            _isCompleted = true;
            _signal.Release();
        }
    }

    public static class Helper
    {
        public static IPortForwarder PortForwarder { get; set; }

        public static bool MarshalCallbacksToMainThreadByDefault { get; set; } = true;

        public static void InstallUnityMainThread()
        {
            UnityThreadDispatcher.InstallFromCurrentContext();
        }

        public static class Crypto
        {
            private const int AesBlockSize = 16;
            private const int HmacSize = 32;
            private const byte PayloadVersion = 1;

            public static (string publicK, string privateK) GenerateRSAKeys()
            {
                using (RSA rsa = RSA.Create())
                {
                    rsa.KeySize = 2048;
                    RSAParameters publicParameters = rsa.ExportParameters(false);
                    RSAParameters privateParameters = rsa.ExportParameters(true);

                    return
                    (
                        ToXmlString(publicParameters, false),
                        ToXmlString(privateParameters, true)
                    );
                }
            }

            public static byte[] EncryptRSA(string publicKeyXml, byte[] data)
            {
                if (string.IsNullOrEmpty(publicKeyXml) || publicKeyXml.IndexOf("<RSAKeyValue>", StringComparison.Ordinal) != 0)
                {
                    throw new ArgumentException("Invalid RSA Public Key format. Expected XML string.", nameof(publicKeyXml));
                }

                if (data == null || data.Length == 0)
                {
                    throw new ArgumentNullException(nameof(data), "RSA encryption failed: data buffer is null or empty.");
                }

                using (RSA rsa = RSA.Create())
                {
                    rsa.ImportParameters(FromXmlString(publicKeyXml));
                    return rsa.Encrypt(data, RSAEncryptionPadding.OaepSHA1);
                }
            }

            public static byte[] DecryptRSA(string privateKey, byte[] data)
            {
                if (string.IsNullOrEmpty(privateKey))
                {
                    throw new ArgumentException("Private key is null or empty.", nameof(privateKey));
                }

                if (data == null || data.Length == 0)
                {
                    throw new ArgumentNullException(nameof(data), "RSA decryption failed: data buffer is null or empty.");
                }

                using (RSA rsa = RSA.Create())
                {
                    rsa.ImportParameters(FromXmlString(privateKey));
                    return rsa.Decrypt(data, RSAEncryptionPadding.OaepSHA1);
                }
            }

            public static byte[] EncryptAES(byte[] data, byte[] key)
            {
                if (data == null)
                {
                    throw new ArgumentNullException(nameof(data));
                }

                if (key == null || key.Length == 0)
                {
                    throw new ArgumentNullException(nameof(key));
                }

                byte[] aesKey;
                byte[] hmacKey;
                DeriveKeys(key, out aesKey, out hmacKey);

                byte[] iv = new byte[AesBlockSize];
                FillRandomBytes(iv);

                byte[] cipherText;
                using (Aes aes = Aes.Create())
                {
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.KeySize = 256;
                    aes.BlockSize = 128;
                    aes.Key = aesKey;
                    aes.IV = iv;

                    using (ICryptoTransform encryptor = aes.CreateEncryptor())
                    {
                        cipherText = encryptor.TransformFinalBlock(data, 0, data.Length);
                    }
                }

                byte[] headerAndCipher = new byte[1 + iv.Length + cipherText.Length];
                headerAndCipher[0] = PayloadVersion;
                Buffer.BlockCopy(iv, 0, headerAndCipher, 1, iv.Length);
                Buffer.BlockCopy(cipherText, 0, headerAndCipher, 1 + iv.Length, cipherText.Length);

                byte[] tag;
                using (HMACSHA256 hmac = new HMACSHA256(hmacKey))
                {
                    tag = hmac.ComputeHash(headerAndCipher);
                }

                byte[] result = new byte[headerAndCipher.Length + tag.Length];
                Buffer.BlockCopy(headerAndCipher, 0, result, 0, headerAndCipher.Length);
                Buffer.BlockCopy(tag, 0, result, headerAndCipher.Length, tag.Length);
                return result;
            }

            public static byte[] DecryptAES(byte[] encryptedData, byte[] key)
            {
                if (encryptedData == null || key == null || key.Length == 0)
                {
                    return null;
                }

                if (encryptedData.Length < 1 + AesBlockSize + HmacSize + 1)
                {
                    return null;
                }

                byte version = encryptedData[0];
                if (version != PayloadVersion)
                {
                    return null;
                }

                int cipherLength = encryptedData.Length - 1 - AesBlockSize - HmacSize;
                if (cipherLength <= 0)
                {
                    return null;
                }

                byte[] aesKey;
                byte[] hmacKey;
                DeriveKeys(key, out aesKey, out hmacKey);

                byte[] headerAndCipher = new byte[1 + AesBlockSize + cipherLength];
                Buffer.BlockCopy(encryptedData, 0, headerAndCipher, 0, headerAndCipher.Length);

                byte[] expectedTag = new byte[HmacSize];
                Buffer.BlockCopy(encryptedData, headerAndCipher.Length, expectedTag, 0, HmacSize);

                byte[] actualTag;
                using (HMACSHA256 hmac = new HMACSHA256(hmacKey))
                {
                    actualTag = hmac.ComputeHash(headerAndCipher);
                }

                if (!FixedTimeEquals(expectedTag, actualTag))
                {
                    return null;
                }

                byte[] iv = new byte[AesBlockSize];
                Buffer.BlockCopy(encryptedData, 1, iv, 0, iv.Length);

                byte[] cipherText = new byte[cipherLength];
                Buffer.BlockCopy(encryptedData, 1 + AesBlockSize, cipherText, 0, cipherLength);

                try
                {
                    using (Aes aes = Aes.Create())
                    {
                        aes.Mode = CipherMode.CBC;
                        aes.Padding = PaddingMode.PKCS7;
                        aes.KeySize = 256;
                        aes.BlockSize = 128;
                        aes.Key = aesKey;
                        aes.IV = iv;

                        using (ICryptoTransform decryptor = aes.CreateDecryptor())
                        {
                            return decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
                        }
                    }
                }
                catch (CryptographicException)
                {
                    return null;
                }
            }

            private static void DeriveKeys(byte[] masterKey, out byte[] aesKey, out byte[] hmacKey)
            {
                byte[] encSuffix = Encoding.UTF8.GetBytes("ENC");
                byte[] macSuffix = Encoding.UTF8.GetBytes("MAC");

                using (SHA256 sha = SHA256.Create())
                {
                    aesKey = sha.ComputeHash(Combine(masterKey, encSuffix));
                    hmacKey = sha.ComputeHash(Combine(masterKey, macSuffix));
                }
            }

            private static byte[] Combine(byte[] left, byte[] right)
            {
                byte[] buffer = new byte[left.Length + right.Length];
                Buffer.BlockCopy(left, 0, buffer, 0, left.Length);
                Buffer.BlockCopy(right, 0, buffer, left.Length, right.Length);
                return buffer;
            }

            private static bool FixedTimeEquals(byte[] left, byte[] right)
            {
                if (left == null || right == null || left.Length != right.Length)
                {
                    return false;
                }

                int diff = 0;
                for (int i = 0; i < left.Length; i++)
                {
                    diff |= left[i] ^ right[i];
                }

                return diff == 0;
            }

            private static void FillRandomBytes(byte[] buffer)
            {
                using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(buffer);
                }
            }

            private static string ToXmlString(RSAParameters parameters, bool includePrivateParameters)
            {
                StringBuilder builder = new StringBuilder();
                builder.Append("<RSAKeyValue>");
                AppendXmlElement(builder, "Modulus", parameters.Modulus);
                AppendXmlElement(builder, "Exponent", parameters.Exponent);

                if (includePrivateParameters)
                {
                    AppendXmlElement(builder, "P", parameters.P);
                    AppendXmlElement(builder, "Q", parameters.Q);
                    AppendXmlElement(builder, "DP", parameters.DP);
                    AppendXmlElement(builder, "DQ", parameters.DQ);
                    AppendXmlElement(builder, "InverseQ", parameters.InverseQ);
                    AppendXmlElement(builder, "D", parameters.D);
                }

                builder.Append("</RSAKeyValue>");
                return builder.ToString();
            }

            private static void AppendXmlElement(StringBuilder builder, string name, byte[] value)
            {
                if (value == null)
                {
                    return;
                }

                builder.Append('<').Append(name).Append('>');
                builder.Append(Convert.ToBase64String(value));
                builder.Append("</").Append(name).Append('>');
            }

            private static RSAParameters FromXmlString(string xml)
            {
                XmlDocument document = new XmlDocument();
                document.LoadXml(xml);

                XmlElement root = document.DocumentElement;
                if (root == null || !string.Equals(root.Name, "RSAKeyValue", StringComparison.Ordinal))
                {
                    throw new CryptographicException("Invalid RSA XML format.");
                }

                RSAParameters parameters = new RSAParameters();
                parameters.Modulus = ReadRequiredElement(root, "Modulus");
                parameters.Exponent = ReadRequiredElement(root, "Exponent");
                parameters.P = ReadOptionalElement(root, "P");
                parameters.Q = ReadOptionalElement(root, "Q");
                parameters.DP = ReadOptionalElement(root, "DP");
                parameters.DQ = ReadOptionalElement(root, "DQ");
                parameters.InverseQ = ReadOptionalElement(root, "InverseQ");
                parameters.D = ReadOptionalElement(root, "D");
                return parameters;
            }

            private static byte[] ReadRequiredElement(XmlElement root, string name)
            {
                XmlNode node = root.SelectSingleNode(name);
                if (node == null || string.IsNullOrEmpty(node.InnerText))
                {
                    throw new CryptographicException("Missing RSA XML element: " + name);
                }

                return Convert.FromBase64String(node.InnerText);
            }

            private static byte[] ReadOptionalElement(XmlElement root, string name)
            {
                XmlNode node = root.SelectSingleNode(name);
                if (node == null || string.IsNullOrEmpty(node.InnerText))
                {
                    return null;
                }

                return Convert.FromBase64String(node.InnerText);
            }
        }

        public static async Task SendFramedPayload(NetworkStream stream, byte[] data)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            byte[] lengthPrefix = BitConverter.GetBytes(data.Length);
            await stream.WriteAsync(lengthPrefix, 0, lengthPrefix.Length).ConfigureAwait(false);
            await stream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
        }

        public static async Task<byte[]> ReadFramedPayload(NetworkStream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            byte[] lengthBuffer = new byte[4];
            int bytesRead = 0;

            while (bytesRead < 4)
            {
                int read = await stream.ReadAsync(lengthBuffer, bytesRead, 4 - bytesRead).ConfigureAwait(false);
                if (read == 0)
                {
                    return null;
                }

                bytesRead += read;
            }

            int payloadLength = BitConverter.ToInt32(lengthBuffer, 0);
            if (payloadLength < 0)
            {
                throw new IOException("Invalid payload length.");
            }

            byte[] payload = new byte[payloadLength];
            int totalReceived = 0;

            while (totalReceived < payloadLength)
            {
                int read = await stream.ReadAsync(payload, totalReceived, payloadLength - totalReceived).ConfigureAwait(false);
                if (read == 0)
                {
                    return null;
                }

                totalReceived += read;
            }

            return payload;
        }

        public static async Task<string> GetIPv4()
        {
            WebRequest request = WebRequest.Create("https://api.ipify.org");

            using (WebResponse response = await request.GetResponseAsync().ConfigureAwait(false))
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                string publicIp = await reader.ReadToEndAsync().ConfigureAwait(false);
                return publicIp.Trim();
            }
        }

        public static async Task<bool> PortForward(int port, Protocol protocol, int delay = 2000, string description = "SamLib Unity Server", bool doDebug = true)
        {
            if (!UnityPlatform.SupportsRawSockets)
            {
                UnityNetLog.Error(doDebug, "[NAT Error] " + UnityPlatform.GetRawSocketError("Port forwarding"));
                return false;
            }

            if (PortForwarder != null)
            {
                return await PortForwarder.TryForwardPortAsync(port, protocol, delay, description, doDebug).ConfigureAwait(false);
            }

#if SAMLIB_OPEN_NAT
            try
            {
                NatDiscoverer discoverer = new NatDiscoverer();
                CancellationTokenSource cts = new CancellationTokenSource(5000);
                Open.Nat.Protocol openNatProtocol = protocol == Protocol.Tcp ? Open.Nat.Protocol.Tcp : Open.Nat.Protocol.Udp;
                NatDevice device = await discoverer.DiscoverDeviceAsync(PortMapper.Upnp | PortMapper.Pmp, cts).ConfigureAwait(false);

                try
                {
                    await device.DeletePortMapAsync(new Mapping(openNatProtocol, port, port, description)).ConfigureAwait(false);
                    UnityNetLog.Info(doDebug, "[NAT] Sent delete request.");
                }
                catch
                {
                }

                await Task.Delay(delay).ConfigureAwait(false);
                string uniqueDescription = description + " " + DateTime.Now.Ticks;
                await device.CreatePortMapAsync(new Mapping(openNatProtocol, port, port, uniqueDescription)).ConfigureAwait(false);
                UnityNetLog.Info(doDebug, "[NAT] Port " + port + " forwarded successfully.");
                return true;
            }
            catch (Exception ex)
            {
                UnityNetLog.Error(doDebug, "[NAT Error] " + ex.Message);
                return false;
            }
#else
            UnityNetLog.Info(doDebug, "[NAT] No port forwarder configured. Assign Helper.PortForwarder or compile with SAMLIB_OPEN_NAT.");
            return false;
#endif
        }

        public static Task<string> GenerateToken(int length = 32)
        {
            if (length <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "Token length must be greater than zero.");
            }

            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!$%^&*()-=+_[]{}<>,.?";
            char[] output = new char[length];
            byte[] randomBytes = new byte[length * 4];

            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }

            for (int i = 0; i < length; i++)
            {
                int randomValue = BitConverter.ToInt32(randomBytes, i * 4) & int.MaxValue;
                output[i] = chars[randomValue % chars.Length];
            }

            return Task.FromResult(new string(output));
        }
    }
    #region TCP
    public class TcpNetServer : IDisposable
    {
        private sealed class ConnectedClient
        {
            public TcpClient Client { get; set; }
            public AsyncMessageQueue<byte[]> MessageQueue { get; set; }
            public SemaphoreSlim SendLock { get; set; }
            public byte[] AesSessionKey { get; set; }
            public bool IsSecure { get; set; }
        }

        public int _port;
        private TcpListener _listener;
        private readonly int _maxClients;
        public bool _started = false;
        public bool _doDebug = false;
        public bool DispatchCallbacksToMainThread { get; set; }

        private readonly TaskCompletionSource<bool> _startTcs = new TaskCompletionSource<bool>();
        private readonly ConcurrentDictionary<string, ConnectedClient> _clients = new ConcurrentDictionary<string, ConnectedClient>();
        private readonly AsyncMessageQueue<(string clientId, byte[] data)> _msgChannel = new AsyncMessageQueue<(string clientId, byte[] data)>();
        private readonly AsyncMessageQueue<string> _connectionChannel = new AsyncMessageQueue<string>();
        private readonly CancellationTokenSource _lifetimeCts = new CancellationTokenSource();

        private readonly bool _doEncrypt;
        private string _privKey;
        private string _pubKey;

        public Action<string, byte[]> OnMessageReceived { get; set; }
        public Action<string> OnClientConnected { get; set; }
        public Action<string> OnClientDisconnected { get; set; }

        public TcpNetServer(int port = 8080, bool doEncrypt = false, bool doDebug = true, int maxClients = 1)
        {
            UnityThreadDispatcher.TryInstallFromCurrentContext();

            _port = port;
            _maxClients = Math.Max(1, maxClients);
            _doDebug = doDebug;
            _doEncrypt = doEncrypt;
            DispatchCallbacksToMainThread = Helper.MarshalCallbacksToMainThreadByDefault;

            if (_doEncrypt)
            {
                (string publicKey, string privateKey) = Helper.Crypto.GenerateRSAKeys();
                _pubKey = publicKey;
                _privKey = privateKey;
            }
        }

        public async Task WaitForStart()
        {
            if (_started)
            {
                return;
            }

            await _startTcs.Task.ConfigureAwait(false);
        }

        public async Task<string> GetServerAddress()
        {
            return await Helper.GetIPv4().ConfigureAwait(false) + ":" + _port;
        }

        public async Task StartServer(string description = "server", int maxAttempts = 5, int delay = 2000, bool doPortForward = true)
        {
            UnityPlatform.ThrowIfRawSocketsUnsupported("TCP server");

            if (_started)
            {
                return;
            }

            int attempt = 0;
            bool success = false;
            int tries = Math.Max(1, maxAttempts);

            while ((!success && attempt < tries) && doPortForward)
            {
                success = await Helper.PortForward(_port, Protocol.Tcp, delay, description, _doDebug).ConfigureAwait(false);
                if (success)
                {
                    break;
                }

                attempt++;
                _port++;
            }

            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listener.Start();

            _started = true;
            _startTcs.TrySetResult(true);
            UnityNetLog.Info(_doDebug, "TCP server started on " + _port + ".");

            _ = AcceptLoop();
        }

        private async Task AcceptLoop()
        {
            try
            {
                while (!_lifetimeCts.IsCancellationRequested)
                {
                    TcpClient client;

                    try
                    {
                        client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (SocketException)
                    {
                        if (_lifetimeCts.IsCancellationRequested)
                        {
                            break;
                        }

                        throw;
                    }

                    if (_clients.Count >= _maxClients)
                    {
                        UnityNetLog.Info(_doDebug, "TCP server full. Rejecting connection.");
                        client.Close();
                        client.Dispose();
                        continue;
                    }

                    ConnectedClient clientInfo = new ConnectedClient
                    {
                        Client = client,
                        MessageQueue = new AsyncMessageQueue<byte[]>(100),
                        SendLock = new SemaphoreSlim(1, 1),
                        IsSecure = false
                    };

                    string clientId = client.Client.RemoteEndPoint != null
                        ? client.Client.RemoteEndPoint.ToString()
                        : Guid.NewGuid().ToString("N");

                    if (_clients.TryAdd(clientId, clientInfo))
                    {
                        UnityNetLog.Info(_doDebug, "TCP client connected: " + clientId + ". Total: " + _clients.Count);
                        _ = HandleClientAsync(clientId, clientInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                UnityNetLog.Error(_doDebug, "TCP server accept loop stopped: " + ex.Message);
            }
        }

        private async Task HandleClientAsync(string clientId, ConnectedClient clientInfo)
        {
            try
            {
                using (NetworkStream stream = clientInfo.Client.GetStream())
                {
                    if (_doEncrypt)
                    {
                        byte[] publicKeyBytes = Encoding.UTF8.GetBytes("PUBKEY:" + _pubKey);
                        await Helper.SendFramedPayload(stream, publicKeyBytes).ConfigureAwait(false);

                        byte[] encryptedKeyResponse = await Helper.ReadFramedPayload(stream).ConfigureAwait(false);
                        if (encryptedKeyResponse == null)
                        {
                            throw new IOException("Client disconnected during encryption handshake.");
                        }

                        string responseStr = Encoding.UTF8.GetString(encryptedKeyResponse);
                        if (!responseStr.StartsWith("AESKEY:", StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("Client sent an invalid handshake response.");
                        }

                        string base64Data = responseStr.Substring(7);
                        if (string.IsNullOrWhiteSpace(base64Data))
                        {
                            throw new InvalidOperationException("Client sent an empty AES key.");
                        }

                        byte[] encryptedAesKey = Convert.FromBase64String(base64Data);
                        clientInfo.AesSessionKey = Helper.Crypto.DecryptRSA(_privKey, encryptedAesKey);
                        clientInfo.IsSecure = true;
                        UnityNetLog.Info(_doDebug, "[Security] Secure TCP session established for " + clientId + ".");
                    }

                    Dispatch(delegate
                    {
                        if (OnClientConnected != null)
                        {
                            OnClientConnected(clientId);
                        }
                    }, "OnClientConnected");

                    _connectionChannel.Enqueue(clientId);

                    while (!_lifetimeCts.IsCancellationRequested)
                    {
                        byte[] receivedData = await Helper.ReadFramedPayload(stream).ConfigureAwait(false);
                        if (receivedData == null)
                        {
                            break;
                        }

                        if (_doEncrypt && clientInfo.IsSecure)
                        {
                            receivedData = Helper.Crypto.DecryptAES(receivedData, clientInfo.AesSessionKey);
                            if (receivedData == null)
                            {
                                UnityNetLog.Error(_doDebug, "[Security] Decryption failed for " + clientId + ". Dropping packet.");
                                continue;
                            }
                        }

                        NotifyMessageReceived(clientId, receivedData);
                        clientInfo.MessageQueue.Enqueue(receivedData);
                    }
                }
            }
            catch (Exception ex)
            {
                UnityNetLog.Error(_doDebug, "TCP error with client " + clientId + ": " + ex.Message);
            }
            finally
            {
                CleanupClient(clientId);
            }
        }

        public async Task Broadcast(string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            List<string> clientIds = _clients.Keys.ToList();

            for (int i = 0; i < clientIds.Count; i++)
            {
                await SendToClient(clientIds[i], data).ConfigureAwait(false);
            }
        }

        public async Task<bool> SendToClient(string clientId, string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            return await SendToClient(clientId, data).ConfigureAwait(false);
        }

        public async Task<bool> SendToClient(string clientId, byte[] data)
        {
            if (!_clients.TryGetValue(clientId, out ConnectedClient clientInfo))
            {
                return false;
            }

            if (data == null)
            {
                return false;
            }

            try
            {
                byte[] dataToSend = data;
                if (_doEncrypt && clientInfo.IsSecure && clientInfo.AesSessionKey != null)
                {
                    dataToSend = Helper.Crypto.EncryptAES(data, clientInfo.AesSessionKey);
                }

                await clientInfo.SendLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    NetworkStream stream = clientInfo.Client.GetStream();
                    await Helper.SendFramedPayload(stream, dataToSend).ConfigureAwait(false);
                }
                finally
                {
                    clientInfo.SendLock.Release();
                }

                return true;
            }
            catch (Exception ex)
            {
                UnityNetLog.Error(_doDebug, "[TCP] Send failed to " + clientId + ": " + ex.Message);
                CleanupClient(clientId);
                return false;
            }
        }

        public Task<string> WaitForClientToConnectAsync(CancellationToken ct = default(CancellationToken))
        {
            return _connectionChannel.DequeueAsync(ct);
        }

        private void NotifyMessageReceived(string clientId, byte[] data)
        {
            _msgChannel.Enqueue((clientId, data));

            Dispatch(delegate
            {
                if (OnMessageReceived != null)
                {
                    OnMessageReceived(clientId, data);
                }
            }, "OnMessageReceived");
        }

        public Task<(string clientId, byte[] data)> WaitForNextMessageAsyncFull()
        {
            return _msgChannel.DequeueAsync();
        }

        public async Task<string> WaitForNextMessageAsync()
        {
            (string clientId, byte[] data) message = await _msgChannel.DequeueAsync().ConfigureAwait(false);
            return Encoding.UTF8.GetString(message.data);
        }

        public async Task<string> WaitForMessageFromClientAsync(string targetClientId)
        {
            if (_clients.TryGetValue(targetClientId, out ConnectedClient clientInfo))
            {
                byte[] data = await clientInfo.MessageQueue.DequeueAsync().ConfigureAwait(false);
                return Encoding.UTF8.GetString(data);
            }

            throw new Exception("Client not found.");
        }

        public Task ListClient()
        {
            foreach (string key in _clients.Keys)
            {
                UnityNetLog.Info(true, key);
            }

            return Task.CompletedTask;
        }

        public Task<List<string>> GetAllClients()
        {
            return Task.FromResult(_clients.Keys.ToList());
        }

        public void StopServer()
        {
            if (!_started)
            {
                return;
            }

            _started = false;
            _lifetimeCts.Cancel();

            if (_listener != null)
            {
                _listener.Stop();
            }

            foreach (string clientId in _clients.Keys.ToList())
            {
                CleanupClient(clientId);
            }

            _msgChannel.Complete();
            _connectionChannel.Complete();
            UnityNetLog.Info(_doDebug, "TCP server stopped.");
        }

        public void Dispose()
        {
            StopServer();
        }

        private void CleanupClient(string clientId)
        {
            if (_clients.TryRemove(clientId, out ConnectedClient removedClient))
            {
                removedClient.MessageQueue.Complete();

                try
                {
                    removedClient.Client.Close();
                }
                catch
                {
                }

                try
                {
                    removedClient.Client.Dispose();
                }
                catch
                {
                }

                try
                {
                    removedClient.SendLock.Dispose();
                }
                catch
                {
                }

                Dispatch(delegate
                {
                    if (OnClientDisconnected != null)
                    {
                        OnClientDisconnected(clientId);
                    }
                }, "OnClientDisconnected");

                UnityNetLog.Info(_doDebug, "TCP client disconnected: " + clientId + ".");
            }
        }

        private void Dispatch(Action callback, string callbackName)
        {
            UnityThreadDispatcher.Invoke(callback, DispatchCallbacksToMainThread, _doDebug, callbackName);
        }
    }

    public class TcpNetClient : IDisposable
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private readonly string _hostname;
        private readonly int _port;
        private bool _isConnected;
        private readonly bool _doDebug;
        private readonly bool _doEncrypt;
        private byte[] _aesSessionKey;
        private bool _isSecure;
        private readonly AsyncMessageQueue<byte[]> _msgChannel = new AsyncMessageQueue<byte[]>();
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

        public bool DispatchCallbacksToMainThread { get; set; }
        public Action<byte[]> OnMessageReceived { get; set; }
        public Action OnConnected { get; set; }
        public Action OnDisconnected { get; set; }

        public TcpNetClient(string hostname = "127.0.0.1", int port = 8080, bool doEncrypt = false, bool doDebug = false)
        {
            UnityThreadDispatcher.TryInstallFromCurrentContext();

            _hostname = hostname;
            _port = port;
            _doEncrypt = doEncrypt;
            _doDebug = doDebug;
            DispatchCallbacksToMainThread = Helper.MarshalCallbacksToMainThreadByDefault;
        }

        public async Task<bool> Connect(bool allowRetry = false, int maxRetry = 1)
        {
            if (!UnityPlatform.SupportsRawSockets)
            {
                UnityNetLog.Error(_doDebug, UnityPlatform.GetRawSocketError("TCP client"));
                return false;
            }

            int attempts = 0;
            int limit = allowRetry ? Math.Max(1, maxRetry) : 1;

            while (attempts < limit)
            {
                try
                {
                    _isConnected = false;
                    _isSecure = false;

                    if (_client != null)
                    {
                        _client.Dispose();
                    }

                    _client = new TcpClient();
                    await _client.ConnectAsync(_hostname, _port).ConfigureAwait(false);
                    _stream = _client.GetStream();

                    if (_doEncrypt && !await PerformHandshake().ConfigureAwait(false))
                    {
                        Disconnect();
                        return false;
                    }

                    _isConnected = true;
                    Dispatch(delegate
                    {
                        if (OnConnected != null)
                        {
                            OnConnected();
                        }
                    }, "OnConnected");

                    _ = ReceiveLoop();
                    return true;
                }
                catch (Exception ex)
                {
                    attempts++;
                    UnityNetLog.Error(_doDebug, "TCP connection failed: " + ex.Message);

                    if (attempts < limit)
                    {
                        await Task.Delay(1000).ConfigureAwait(false);
                    }
                }
            }

            return false;
        }

        private async Task<bool> PerformHandshake()
        {
            try
            {
                byte[] keyData = await Helper.ReadFramedPayload(_stream).ConfigureAwait(false);
                if (keyData == null)
                {
                    throw new IOException("Server closed during handshake.");
                }

                string keyMsg = Encoding.UTF8.GetString(keyData);
                if (!keyMsg.StartsWith("PUBKEY:", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Invalid handshake.");
                }

                string publicKey = keyMsg.Substring(7);
                _aesSessionKey = new byte[32];
                using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(_aesSessionKey);
                }

                byte[] encryptedAesKey = Helper.Crypto.EncryptRSA(publicKey, _aesSessionKey);
                string response = "AESKEY:" + Convert.ToBase64String(encryptedAesKey);
                byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                await Helper.SendFramedPayload(_stream, responseBytes).ConfigureAwait(false);

                _isSecure = true;
                UnityNetLog.Info(_doDebug, "[Security] TCP encrypted session established.");
                return true;
            }
            catch (Exception ex)
            {
                UnityNetLog.Error(_doDebug, "[Security] TCP handshake failed: " + ex.Message);
                return false;
            }
        }

        private void NotifyMessageReceived(byte[] data)
        {
            _msgChannel.Enqueue(data);

            Dispatch(delegate
            {
                if (OnMessageReceived != null)
                {
                    OnMessageReceived(data);
                }
            }, "OnMessageReceived");
        }

        public async Task<string> WaitForNextMessageAsync()
        {
            byte[] data = await _msgChannel.DequeueAsync().ConfigureAwait(false);
            return Encoding.UTF8.GetString(data);
        }

        public Task<byte[]> WaitForNextMessageAsyncFull()
        {
            return _msgChannel.DequeueAsync();
        }

        private async Task ReceiveLoop()
        {
            try
            {
                while (_client != null && _client.Connected)
                {
                    byte[] data = await Helper.ReadFramedPayload(_stream).ConfigureAwait(false);
                    if (data == null)
                    {
                        break;
                    }

                    if (_doEncrypt && _isSecure)
                    {
                        data = Helper.Crypto.DecryptAES(data, _aesSessionKey);
                        if (data == null)
                        {
                            continue;
                        }
                    }

                    NotifyMessageReceived(data);
                }
            }
            catch (Exception ex)
            {
                UnityNetLog.Error(_doDebug, "TCP receive loop stopped: " + ex.Message);
            }
            finally
            {
                Disconnect();
            }
        }

        public async Task Send(string message)
        {
            if (!_isConnected || _stream == null)
            {
                return;
            }

            byte[] data = Encoding.UTF8.GetBytes(message);
            await Send(data).ConfigureAwait(false);
        }

        public async Task Send(byte[] data)
        {
            if (!_isConnected || _stream == null || data == null)
            {
                return;
            }

            try
            {
                byte[] dataToSend = data;

                if (_doEncrypt && _isSecure)
                {
                    dataToSend = Helper.Crypto.EncryptAES(data, _aesSessionKey);
                }

                await _sendLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    await Helper.SendFramedPayload(_stream, dataToSend).ConfigureAwait(false);
                }
                finally
                {
                    _sendLock.Release();
                }
            }
            catch
            {
                Disconnect();
            }
        }

        public void Disconnect()
        {
            if (!_isConnected && _client == null && _stream == null)
            {
                return;
            }

            bool wasConnected = _isConnected;
            _isConnected = false;
            _isSecure = false;

            if (_stream != null)
            {
                try
                {
                    _stream.Dispose();
                }
                catch
                {
                }

                _stream = null;
            }

            if (_client != null)
            {
                try
                {
                    _client.Close();
                }
                catch
                {
                }

                try
                {
                    _client.Dispose();
                }
                catch
                {
                }

                _client = null;
            }

            if (wasConnected)
            {
                Dispatch(delegate
                {
                    if (OnDisconnected != null)
                    {
                        OnDisconnected();
                    }
                }, "OnDisconnected");

                UnityNetLog.Info(_doDebug, "TCP client disconnected.");
            }
        }

        public void Dispose()
        {
            Disconnect();
            _msgChannel.Complete();
            _sendLock.Dispose();
        }

        private void Dispatch(Action callback, string callbackName)
        {
            UnityThreadDispatcher.Invoke(callback, DispatchCallbacksToMainThread, _doDebug, callbackName);
        }
    }
    #endregion

    #region UDP
    public class UdpNetServer : IDisposable
    {
        private sealed class ConnectedClient
        {
            public IPEndPoint EndPoint { get; set; }
            public AsyncMessageQueue<byte[]> MessageQueue { get; set; }
            public DateTime LastHeartbeat { get; set; }
        }

        public int _port;
        private UdpClient _udpListener;
        private readonly int _maxClients;
        public bool _started = false;
        public bool _doDebug = false;
        public bool _autoDisconnect = true;
        public int _timeout = 10;
        public bool DispatchCallbacksToMainThread { get; set; }

        private readonly TaskCompletionSource<bool> _startTcs = new TaskCompletionSource<bool>();
        private readonly ConcurrentDictionary<string, ConnectedClient> _clients = new ConcurrentDictionary<string, ConnectedClient>();
        private readonly AsyncMessageQueue<(string clientId, byte[] data)> _msgChannel = new AsyncMessageQueue<(string clientId, byte[] data)>();
        private readonly AsyncMessageQueue<string> _connectionChannel = new AsyncMessageQueue<string>();
        private readonly CancellationTokenSource _lifetimeCts = new CancellationTokenSource();

        public Action<string, byte[]> OnMessageReceived { get; set; }
        public Action<string> OnClientConnected { get; set; }
        public Action<string> OnClientDisconnected { get; set; }

        public UdpNetServer(int port = 8080, bool doDebug = true, bool autoDisconnect = true, int maxClients = 1, int timeOut = 10)
        {
            UnityThreadDispatcher.TryInstallFromCurrentContext();

            _port = port;
            _maxClients = Math.Max(1, maxClients);
            _doDebug = doDebug;
            _autoDisconnect = autoDisconnect;
            _timeout = timeOut;
            DispatchCallbacksToMainThread = Helper.MarshalCallbacksToMainThreadByDefault;
        }

        public async Task WaitForStart()
        {
            if (_started)
            {
                return;
            }

            await _startTcs.Task.ConfigureAwait(false);
        }

        public async Task<string> GetServerAddress()
        {
            return await Helper.GetIPv4().ConfigureAwait(false) + ":" + _port;
        }

        public async Task StartServer(string description = "udp_server", int maxAttempts = 5, int delay = 2000, bool doPortForward = true)
        {
            UnityPlatform.ThrowIfRawSocketsUnsupported("UDP server");

            if (_started)
            {
                return;
            }

            int attempt = 0;
            bool success = false;
            int tries = Math.Max(1, maxAttempts);

            while ((!success && attempt < tries) && doPortForward)
            {
                success = await Helper.PortForward(_port, Protocol.Udp, delay, description, _doDebug).ConfigureAwait(false);
                if (success)
                {
                    break;
                }

                attempt++;
                _port++;
            }

            try
            {
                _udpListener = new UdpClient(_port);
                ConfigureUdpSocket(_udpListener);
            }
            catch (Exception ex)
            {
                UnityNetLog.Error(_doDebug, "Failed to bind UDP server: " + ex.Message);
                return;
            }

            _started = true;
            _startTcs.TrySetResult(true);
            UnityNetLog.Info(_doDebug, "UDP server started on " + _port + ".");

            _ = ReceiveLoop();
            _ = ClientCleanupLoop();
        }

        private async Task ReceiveLoop()
        {
            while (!_lifetimeCts.IsCancellationRequested)
            {
                try
                {
                    UdpReceiveResult result = await _udpListener.ReceiveAsync().ConfigureAwait(false);
                    byte[] data = result.Buffer;
                    IPEndPoint remoteEndPoint = result.RemoteEndPoint;
                    string clientId = remoteEndPoint.ToString();

                    if (!_clients.TryGetValue(clientId, out ConnectedClient clientInfo))
                    {
                        if (_clients.Count >= _maxClients)
                        {
                            UnityNetLog.Info(_doDebug, "UDP server full. Ignoring packet from " + clientId + ".");
                            continue;
                        }

                        clientInfo = new ConnectedClient
                        {
                            EndPoint = remoteEndPoint,
                            MessageQueue = new AsyncMessageQueue<byte[]>(100),
                            LastHeartbeat = DateTime.UtcNow
                        };

                        if (_clients.TryAdd(clientId, clientInfo))
                        {
                            Dispatch(delegate
                            {
                                if (OnClientConnected != null)
                                {
                                    OnClientConnected(clientId);
                                }
                            }, "OnClientConnected");

                            _connectionChannel.Enqueue(clientId);
                            UnityNetLog.Info(_doDebug, "UDP client connected: " + clientId + ". Total: " + _clients.Count);
                        }
                    }

                    clientInfo.LastHeartbeat = DateTime.UtcNow;

                    if (data != null && data.Length > 0)
                    {
                        NotifyMessageReceived(clientId, data);
                        clientInfo.MessageQueue.Enqueue(data);
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException)
                {
                    if (_lifetimeCts.IsCancellationRequested)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    UnityNetLog.Error(_doDebug, "UDP receive error: " + ex.Message);
                }
            }
        }

        private async Task ClientCleanupLoop()
        {
            while (_started && !_lifetimeCts.IsCancellationRequested)
            {
                await Task.Delay(5000).ConfigureAwait(false);

                if (!_autoDisconnect)
                {
                    continue;
                }

                DateTime now = DateTime.UtcNow;
                TimeSpan timeout = TimeSpan.FromSeconds(_timeout);

                foreach (KeyValuePair<string, ConnectedClient> client in _clients.ToList())
                {
                    if (now - client.Value.LastHeartbeat > timeout)
                    {
                        CleanupClient(client.Key, "UDP client timed out: " + client.Key);
                    }
                }
            }
        }

        public async Task Broadcast(string message)
        {
            if (_udpListener == null)
            {
                return;
            }

            byte[] data = Encoding.UTF8.GetBytes(message);
            List<ConnectedClient> clients = _clients.Values.ToList();

            for (int i = 0; i < clients.Count; i++)
            {
                await _udpListener.SendAsync(data, data.Length, clients[i].EndPoint).ConfigureAwait(false);
            }
        }

        public async Task<bool> SendToClient(string clientId, string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            return await SendToClient(clientId, data).ConfigureAwait(false);
        }

        public async Task<bool> SendToClient(string clientId, byte[] data)
        {
            if (_udpListener == null || data == null)
            {
                return false;
            }

            if (_clients.TryGetValue(clientId, out ConnectedClient clientInfo))
            {
                try
                {
                    await _udpListener.SendAsync(data, data.Length, clientInfo.EndPoint).ConfigureAwait(false);
                    return true;
                }
                catch
                {
                    CleanupClient(clientId, null);
                    return false;
                }
            }

            return false;
        }

        public Task<string> WaitForClientToConnectAsync(CancellationToken ct = default(CancellationToken))
        {
            return _connectionChannel.DequeueAsync(ct);
        }

        private void NotifyMessageReceived(string clientId, byte[] data)
        {
            _msgChannel.Enqueue((clientId, data));

            Dispatch(delegate
            {
                if (OnMessageReceived != null)
                {
                    OnMessageReceived(clientId, data);
                }
            }, "OnMessageReceived");
        }

        public Task<(string clientId, byte[] data)> WaitForNextMessageAsyncFull()
        {
            return _msgChannel.DequeueAsync();
        }

        public async Task<string> WaitForNextMessageAsync()
        {
            (string clientId, byte[] data) message = await _msgChannel.DequeueAsync().ConfigureAwait(false);
            return Encoding.UTF8.GetString(message.data);
        }

        public async Task<string> WaitForMessageFromClientAsync(string targetClientId)
        {
            if (_clients.TryGetValue(targetClientId, out ConnectedClient clientInfo))
            {
                byte[] data = await clientInfo.MessageQueue.DequeueAsync().ConfigureAwait(false);
                return Encoding.UTF8.GetString(data);
            }

            throw new Exception("Client not found.");
        }

        public Task ListClient()
        {
            foreach (string key in _clients.Keys)
            {
                UnityNetLog.Info(true, key);
            }

            return Task.CompletedTask;
        }

        public Task<List<string>> GetAllClients()
        {
            return Task.FromResult(_clients.Keys.ToList());
        }

        public void StopServer()
        {
            if (!_started)
            {
                return;
            }

            _started = false;
            _lifetimeCts.Cancel();

            if (_udpListener != null)
            {
                try
                {
                    _udpListener.Close();
                }
                catch
                {
                }

                try
                {
                    _udpListener.Dispose();
                }
                catch
                {
                }

                _udpListener = null;
            }

            foreach (string clientId in _clients.Keys.ToList())
            {
                CleanupClient(clientId, null);
            }

            _msgChannel.Complete();
            _connectionChannel.Complete();
            UnityNetLog.Info(_doDebug, "UDP server stopped.");
        }

        public void Dispose()
        {
            StopServer();
        }

        private void CleanupClient(string clientId, string logMessage)
        {
            if (_clients.TryRemove(clientId, out ConnectedClient removed))
            {
                removed.MessageQueue.Complete();

                Dispatch(delegate
                {
                    if (OnClientDisconnected != null)
                    {
                        OnClientDisconnected(clientId);
                    }
                }, "OnClientDisconnected");

                if (!string.IsNullOrEmpty(logMessage))
                {
                    UnityNetLog.Info(_doDebug, logMessage);
                }
            }
        }

        private static void ConfigureUdpSocket(UdpClient client)
        {
            if (client == null || client.Client == null)
            {
                return;
            }

            try
            {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    client.Client.IOControl((IOControlCode)(-1744830452), new byte[] { 0, 0, 0, 0 }, null);
                }
            }
            catch
            {
            }
        }

        private void Dispatch(Action callback, string callbackName)
        {
            UnityThreadDispatcher.Invoke(callback, DispatchCallbacksToMainThread, _doDebug, callbackName);
        }
    }

    public class UdpNetClient : IDisposable
    {
        private UdpClient _client;
        private readonly string _hostname;
        private readonly int _port;
        private bool _isConnected;
        private readonly bool _doDebug;
        private readonly AsyncMessageQueue<byte[]> _msgChannel = new AsyncMessageQueue<byte[]>();

        public bool DispatchCallbacksToMainThread { get; set; }
        public Action<byte[]> OnMessageReceived { get; set; }
        public Action OnConnected { get; set; }
        public Action OnDisconnected { get; set; }

        public UdpNetClient(string hostname = "127.0.0.1", int port = 8080, bool doDebug = false)
        {
            UnityThreadDispatcher.TryInstallFromCurrentContext();

            _hostname = hostname;
            _port = port;
            _doDebug = doDebug;
            DispatchCallbacksToMainThread = Helper.MarshalCallbacksToMainThreadByDefault;
        }

        public async Task<bool> Connect(bool allowRetry = false, int maxRetry = 1)
        {
            if (!UnityPlatform.SupportsRawSockets)
            {
                UnityNetLog.Error(_doDebug, UnityPlatform.GetRawSocketError("UDP client"));
                return false;
            }

            int attempts = 0;
            int limit = allowRetry ? Math.Max(1, maxRetry) : 1;

            while (attempts < limit)
            {
                try
                {
                    if (_client != null)
                    {
                        _client.Dispose();
                    }

                    _client = new UdpClient();
                    ConfigureUdpSocket(_client);
                    _client.Connect(_hostname, _port);

                    _isConnected = true;

                    Dispatch(delegate
                    {
                        if (OnConnected != null)
                        {
                            OnConnected();
                        }
                    }, "OnConnected");

                    _ = ReceiveLoop();

                    byte[] handshake = Encoding.UTF8.GetBytes("HELLO_SERVER");
                    await Send(handshake).ConfigureAwait(false);
                    return true;
                }
                catch (Exception ex)
                {
                    attempts++;
                    UnityNetLog.Error(_doDebug, "UDP connect failed: " + ex.Message);

                    if (attempts < limit)
                    {
                        await Task.Delay(1000).ConfigureAwait(false);
                    }
                }
            }

            return false;
        }

        private void NotifyMessageReceived(byte[] data)
        {
            _msgChannel.Enqueue(data);

            Dispatch(delegate
            {
                if (OnMessageReceived != null)
                {
                    OnMessageReceived(data);
                }
            }, "OnMessageReceived");
        }

        public async Task<string> WaitForNextMessageAsync()
        {
            byte[] data = await _msgChannel.DequeueAsync().ConfigureAwait(false);
            return Encoding.UTF8.GetString(data);
        }

        public Task<byte[]> WaitForNextMessageAsyncFull()
        {
            return _msgChannel.DequeueAsync();
        }

        private async Task ReceiveLoop()
        {
            try
            {
                while (_isConnected)
                {
                    UdpReceiveResult result = await _client.ReceiveAsync().ConfigureAwait(false);
                    byte[] data = result.Buffer;

                    if (data != null && data.Length > 0)
                    {
                        NotifyMessageReceived(data);
                    }
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                UnityNetLog.Error(_doDebug, "UDP receive error: " + ex.Message);
            }
            finally
            {
                Disconnect();
            }
        }

        public async Task Send(string message)
        {
            if (!_isConnected)
            {
                return;
            }

            byte[] data = Encoding.UTF8.GetBytes(message);
            await Send(data).ConfigureAwait(false);
        }

        public async Task Send(byte[] data)
        {
            if (!_isConnected || _client == null || data == null)
            {
                return;
            }

            try
            {
                await _client.SendAsync(data, data.Length).ConfigureAwait(false);
            }
            catch
            {
                Disconnect();
            }
        }

        public void Disconnect()
        {
            if (!_isConnected && _client == null)
            {
                return;
            }

            bool wasConnected = _isConnected;
            _isConnected = false;

            if (_client != null)
            {
                try
                {
                    _client.Close();
                }
                catch
                {
                }

                try
                {
                    _client.Dispose();
                }
                catch
                {
                }

                _client = null;
            }

            if (wasConnected)
            {
                Dispatch(delegate
                {
                    if (OnDisconnected != null)
                    {
                        OnDisconnected();
                    }
                }, "OnDisconnected");

                UnityNetLog.Info(_doDebug, "UDP client disconnected.");
            }
        }

        public void Dispose()
        {
            Disconnect();
            _msgChannel.Complete();
        }

        private static void ConfigureUdpSocket(UdpClient client)
        {
            if (client == null || client.Client == null)
            {
                return;
            }

            try
            {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    client.Client.IOControl((IOControlCode)(-1744830452), new byte[] { 0, 0, 0, 0 }, null);
                }
            }
            catch
            {
            }
        }

        private void Dispatch(Action callback, string callbackName)
        {
            UnityThreadDispatcher.Invoke(callback, DispatchCallbacksToMainThread, _doDebug, callbackName);
        }
    }
    #endregion

    #region Linker
    public class NetLinker
    {
        private readonly TcpNetServer _tcp;
        private readonly UdpNetServer _udp;
        private readonly bool _debug;
        private readonly ConcurrentDictionary<string, string> _pendingTokens = new ConcurrentDictionary<string, string>();
        private readonly ConcurrentDictionary<string, string> _links = new ConcurrentDictionary<string, string>();

        public NetLinker(TcpNetServer tcp, UdpNetServer udp, bool debug = false)
        {
            _tcp = tcp;
            _udp = udp;
            _debug = debug;
            _udp.OnMessageReceived += HandleUdpMessage;
        }

        public async Task StartLink(string tcpId)
        {
            string token = await Helper.GenerateToken(16).ConfigureAwait(false);
            _pendingTokens[token] = tcpId;
            await _tcp.SendToClient(tcpId, "TOKEN:" + token).ConfigureAwait(false);
        }

        public string GetUdpId(string tcpId)
        {
            return _links.TryGetValue(tcpId, out string id) ? id : null;
        }

        public void Remove(string tcpId)
        {
            _links.TryRemove(tcpId, out _);
        }

        private async void HandleUdpMessage(string udpId, byte[] data)
        {
            string message = Encoding.UTF8.GetString(data);
            if (!message.StartsWith("AUTH:", StringComparison.Ordinal))
            {
                return;
            }

            string token = message.Substring(5);
            if (_pendingTokens.TryRemove(token, out string tcpId))
            {
                _links[tcpId] = udpId;
                await _tcp.SendToClient(tcpId, "AUTH_OK").ConfigureAwait(false);
                UnityNetLog.Info(_debug, "[Linker] Linked TCP " + tcpId + " to UDP " + udpId + ".");
            }
        }
    }

    public class NetLinkerClient
    {
        private readonly TcpNetClient _tcp;
        private readonly UdpNetClient _udp;
        private readonly bool _debug;
        private bool _isLinked;

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

        private void HandleHandshakeMessages(byte[] data)
        {
            string message = Encoding.UTF8.GetString(data);

            if (message.StartsWith("TOKEN:", StringComparison.Ordinal))
            {
                string token = message.Substring(6);
                _ = BeginUdpSpam(token);
            }
            else if (message == "AUTH_OK")
            {
                _isLinked = true;
                _tcp.OnMessageReceived -= HandleHandshakeMessages;
                UnityNetLog.Info(_debug, "[Client] UDP link handshake complete.");
            }
        }

        private async Task BeginUdpSpam(string token)
        {
            _isLinked = false;
            int attempts = 0;

            while (!_isLinked && attempts < 20)
            {
                await _udp.Send("AUTH:" + token).ConfigureAwait(false);
                await Task.Delay(200).ConfigureAwait(false);
                attempts++;
            }

            if (!_isLinked)
            {
                UnityNetLog.Error(_debug, "[Client] UDP link timeout.");
            }
        }
    }
    #endregion
}
