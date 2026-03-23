namespace SamLib.Security
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

    public static class Util
    {
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
    }
}
