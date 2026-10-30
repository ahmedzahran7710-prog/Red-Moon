using Canyon.Network.Services;
using Canyon.Shared.Helpers;
using Org.BouncyCastle.Math;
using System.Security.Cryptography;
using System.Text;

namespace Canyon.Network.Security
{
    /// <summary>
    ///     This implementation of the Diffie Hellman Key Exchange implements the base modulo
    ///     and big number operations without a hash algorithm. This is non-standard, and was
    ///     later fixed in higher versions of Conquer Online using MD5.
    /// </summary>
    public sealed class NDDiffieHellman
    {
        /// <summary>
        ///     Generate the modulus integer as a static constant. This is an unfortunate
        ///     consequence of creating a randomness service for generating numbers in a
        ///     thread safe environment, and using a language with poor multi-threading
        ///     support.
        /// </summary>
        static NDDiffieHellman()
        {
            ProbablePrimes = new PrimeGeneratorService();
        }

        private readonly MD5 md5;

        /// <summary>
        ///     Instantiates a new instance of the <see cref="NDDiffieHellman" /> key exchange.
        ///     If no prime root or generator is specified, then defaults for remaining W
        ///     interoperable with the Conquer Online game client will be used.
        /// </summary>
        /// <param name="p">Prime root to modulo with the generated probable prime.</param>
        /// <param name="g">Generator used to seed the modulo of primes.</param>
        public NDDiffieHellman(
            string p = DefaultPrimativeRoot,
            string g = DefaultGenerator)
        {
            PrimeRoot = new BigInteger(p, 16);
            Generator = new BigInteger(g, 16);
            DecryptionIV = new byte[8];
            EncryptionIV = new byte[8];

            md5 = MD5.Create();
        }

        // Key exchange Properties
        public BigInteger PrimeRoot { get; set; }
        public BigInteger Generator { get; set; }
        public BigInteger Modulus { get; set; }
        public BigInteger PublicKey { get; private set; }
        public BigInteger PrivateKey { get; private set; }

        // Blowfish IV exchange properties
        public byte[] DecryptionIV { get; }
        public byte[] EncryptionIV { get; }

        /// <summary>Computes the public key for sending to the client.</summary>
        public async Task ComputePublicKeyAsync()
        {
            if (Modulus == null)
            {
                Modulus = await ProbablePrimes.NextAsync();
            }

            PublicKey = Generator.ModPow(Modulus, PrimeRoot);
        }

        /// <summary>Computes the private key given the client response.</summary>
        /// <param name="clientKeyString">Client key from the exchange</param>
        /// <returns>Bytes representing the private key for Blowfish Cipher.</returns>
        public void ComputePrivateKey(string clientKeyString)
        {
            if (string.IsNullOrWhiteSpace(clientKeyString) || clientKeyString.Length > 256)
            {
                throw new CryptographicException("Invalid client Diffie-Hellman key.");
            }

            for (int i = 0; i < clientKeyString.Length; i++)
            {
                if (!Uri.IsHexDigit(clientKeyString[i]))
                {
                    throw new CryptographicException("Malformed hex in client Diffie-Hellman key.");
                }
            }

            var clientKey = new BigInteger(clientKeyString, 16);
            BigInteger maxValid = PrimeRoot.Subtract(BigInteger.One);
            if (clientKey.CompareTo(BigInteger.One) <= 0 || clientKey.CompareTo(maxValid) >= 0)
            {
                throw new CryptographicException("Rejected trivial or out-of-range client Diffie-Hellman key.");
            }

            PrivateKey = clientKey.ModPow(Modulus, PrimeRoot);
            if (PrivateKey.CompareTo(BigInteger.One) <= 0)
            {
                throw new CryptographicException("Invalid derived Diffie-Hellman shared secret.");
            }
        }

        public byte[] ProcessDHSecret()
        {
            if (PrivateKey == null)
            {
                throw new CryptographicException("Diffie-Hellman shared secret has not been initialized.");
            }

            byte[] key = PrivateKey.ToByteArrayUnsigned();
            int validLength = key.TakeWhile(x => x != 0).Count();
            string sz1 = md5.ComputeHash(key, 0, validLength).ToHexString();
            string sz2 = md5.ComputeHash(Encoding.ASCII.GetBytes(string.Concat(sz1, sz1))).ToHexString();
            return Encoding.ASCII.GetBytes(string.Concat(sz1, sz2));
        }

        private const string DefaultGenerator = "05";

        private const string DefaultPrimativeRoot =
            "E7A69EBDF105F2A6BBDEAD7E798F76A209AD73FB466431E2E7352ED262F8C558" +
            "F10BEFEA977DE9E21DCEE9B04D245F300ECCBBA03E72630556D011023F9E857F";

        // Constants and static properties
        public static readonly PrimeGeneratorService ProbablePrimes;
    }
}
