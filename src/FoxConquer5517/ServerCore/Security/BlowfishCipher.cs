// Derived from the FoxConquer5517 ICipher contract; MIT attribution remains in LICENSE.
// IMPORTANT: the upstream active implementation calls CAST_set_key/CAST_cfb64_encrypt,
// despite its historical class name. Do not substitute Blowfish or CFB8 here.
using System;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using ServerCore.Interfaces;

namespace ServerCore.Security
{
    /// <summary>Managed CAST5 CFB64, retaining the legacy public class name/API.</summary>
    public sealed class BlowfishCipher : ICipher
    {
        public static byte[] InitialKey;
        public const int BF_BLOCK_SIZE = 8;
        private readonly object _gate = new object();
        private readonly Cast5Engine _block = new Cast5Engine();
        private readonly byte[] _encryptIv = new byte[8];
        private readonly byte[] _decryptIv = new byte[8];
        private int _encryptNum;
        private int _decryptNum;

        public BlowfishCipher() { KeySchedule(InitialKey); }

        public void KeySchedule(byte[] key)
        {
            if (key == null || key.Length == 0) throw new ArgumentException("A protocol key is required.", nameof(key));
            // OpenSSL CAST_set_key takes at most 16 bytes (including a long DH secret).
            var effective = new byte[Math.Min(key.Length, 16)];
            Buffer.BlockCopy(key, 0, effective, 0, effective.Length);
            lock (_gate)
            {
                _block.Init(true, new KeyParameter(effective));
                _encryptNum = _decryptNum = 0;
            }
            Array.Clear(effective, 0, effective.Length);
        }

        public void GenerateKeys(int account, int authentication) { throw new NotSupportedException("Use the DH key schedule."); }

        public bool SetDecryptionIV(byte[] iv) { SetIv(iv, _decryptIv, false); return true; }
        public bool SetEncryptionIV(byte[] iv) { SetIv(iv, _encryptIv, true); return true; }
        private void SetIv(byte[] input, byte[] target, bool encrypt)
        {
            if (input == null || input.Length != 8) throw new ArgumentException("CFB64 IV must contain 8 bytes.", nameof(input));
            lock (_gate)
            {
                Buffer.BlockCopy(input, 0, target, 0, 8);
                if (encrypt) _encryptNum = 0; else _decryptNum = 0;
            }
        }

        public byte[] Encrypt(byte[] packet, int length)
        {
            Validate(packet, length);
            var output = new byte[length];
            Transform(packet, output, length, 0, true);
            return output;
        }

        public byte[] Decrypt(byte[] buffer, int length)
        {
            Validate(buffer, length);
            var output = new byte[length];
            Transform(buffer, output, length, 0, false);
            return output;
        }

        public void Decrypt(byte[] packet, byte[] buffer, int length, int position)
        {
            Validate(buffer, length);
            if (packet == null || position < 0 || position > packet.Length - length)
                throw new ArgumentOutOfRangeException(nameof(position));
            // Overlapping forward output must not replace bytes not yet consumed.
            if (ReferenceEquals(packet, buffer) && position > 0 && position < length)
                buffer = (byte[])buffer.Clone();
            Transform(buffer, packet, length, position, false);
        }

        private static void Validate(byte[] input, int length)
        {
            if (input == null || length < 0 || length > input.Length)
                throw new ArgumentOutOfRangeException(nameof(length));
        }

        private void Transform(byte[] input, byte[] output, int length, int offset, bool encrypt)
        {
            lock (_gate)
            {
                byte[] iv = encrypt ? _encryptIv : _decryptIv;
                int n = encrypt ? _encryptNum : _decryptNum;
                var blockOutput = new byte[8];
                for (int i = 0; i < length; i++)
                {
                    if (n == 0)
                    {
                        _block.ProcessBlock(iv, 0, blockOutput, 0);
                        Buffer.BlockCopy(blockOutput, 0, iv, 0, 8);
                    }
                    byte original = input[i];
                    byte transformed = (byte)(original ^ iv[n]);
                    output[offset + i] = transformed;
                    iv[n] = encrypt ? transformed : original;
                    n = (n + 1) & 7;
                }
                if (encrypt) _encryptNum = n; else _decryptNum = n;
            }
        }
    }
}
