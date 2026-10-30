using System.Security.Cryptography;
using System.Text;

namespace Canyon.Network.Security
{
    public sealed class AesCipher : ICipher
    {
        public readonly static string SharedKey = "8y/B?E(H+MbQeThWmZq3t6w9z$C&F)J@";
        public readonly static string SharedIv = "t6w9z$C&F)J@NcRf";

        private Aes aes;
        private ICryptoTransform mEncryptor;
        private ICryptoTransform mDecryptor;

        private byte[] mEncryptIv;
        private byte[] mDecryptIv;

        private AesCipher()
        {
        }

        [Obsolete("Do not use this method with shared keys or change the Shared Keys to private ones.", false)]
        public static AesCipher Create()
        {
            return Create(SharedKey, SharedIv, SharedIv);
        }

        public static AesCipher Create(string key, string eIv, string dIv)
        {
            var cipher = new AesCipher
            {
                aes = Aes.Create()
            };

            cipher.aes.Mode = CipherMode.CFB;
            cipher.aes.KeySize = 256;
            cipher.aes.BlockSize = 128;
            cipher.aes.FeedbackSize = 8;
            cipher.aes.Padding = PaddingMode.None;

            cipher.aes.Key = Encoding.ASCII.GetBytes(key);
            cipher.mEncryptIv = Encoding.ASCII.GetBytes(eIv);
            cipher.mDecryptIv = Encoding.ASCII.GetBytes(dIv);

            cipher.mEncryptor = cipher.aes.CreateEncryptor(cipher.aes.Key, cipher.mEncryptIv);
            cipher.mDecryptor = cipher.aes.CreateDecryptor(cipher.aes.Key, cipher.mDecryptIv);
            return cipher;
        }

        /// <inheritdoc />
        public void GenerateKeys(object[] seeds)
        {
            if (seeds == null || seeds.Length == 0 || seeds[0] is not byte[] rawKey || rawKey.Length == 0)
            {
                throw new CryptographicException("Invalid key material for AES key generation.");
            }

            byte[] derivedKey = SHA256.HashData(rawKey);
            aes.Key = derivedKey;

            if (seeds.Length > 2 && seeds[1] is byte[] eIv && seeds[2] is byte[] dIv && eIv.Length > 0 && dIv.Length > 0)
            {
                mEncryptIv = eIv.Length == 16 ? (byte[])eIv.Clone() : SHA256.HashData(eIv)[..16];
                mDecryptIv = dIv.Length == 16 ? (byte[])dIv.Clone() : SHA256.HashData(dIv)[..16];
            }
            else
            {
                byte[] iv = Encoding.ASCII.GetBytes(SharedIv);

                mEncryptIv = new byte[16];
                mDecryptIv = new byte[16];

                Buffer.BlockCopy(iv, 0, mEncryptIv, 0, Math.Min(iv.Length, 16));
                Buffer.BlockCopy(iv, 0, mDecryptIv, 0, Math.Min(iv.Length, 16));
            }

            mEncryptor?.Dispose();
            mDecryptor?.Dispose();
            mEncryptor = aes.CreateEncryptor(aes.Key, mEncryptIv);
            mDecryptor = aes.CreateDecryptor(aes.Key, mDecryptIv);
        }

        /// <inheritdoc />
        public void Decrypt(Span<byte> src, Span<byte> dst)
        {
            using MemoryStream memory = new();
            using CryptoStream stream = new(memory, mDecryptor, CryptoStreamMode.Write);
            stream.Write(src);
            stream.FlushFinalBlock();

            byte[] decrypted = memory.ToArray();
            for (int i = 0; i < decrypted.Length; i++)
            {
                dst[i] = decrypted[i];
            }
        }

        /// <inheritdoc />
        public void Encrypt(Span<byte> src, Span<byte> dst)
        {
            using var memory = new MemoryStream();
            using var stream = new CryptoStream(memory, mEncryptor, CryptoStreamMode.Write);
            stream.Write(src);
            stream.FlushFinalBlock();

            byte[] encrypted = memory.ToArray();
            for (int i = 0; i < encrypted.Length; i++)
            {
                dst[i] = encrypted[i];
            }
        }
    }
}
