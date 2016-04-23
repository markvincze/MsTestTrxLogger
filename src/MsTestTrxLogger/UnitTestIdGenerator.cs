using System;
using System.Security.Cryptography;
using System.Text;

namespace MsTestTrxLogger
{
    public static class UnitTestIdGenerator
    {
        private static readonly HashAlgorithm provider  = new SHA1CryptoServiceProvider();

        /// <summary>
        /// Calculates a hash of the string and copies the first 128 bits of the hash to a new Guid.
        /// </summary>
        internal static Guid GuidFromString(string data)
        {
            if (String.IsNullOrEmpty(data))
            {
                throw new ArgumentNullException(nameof(data));
            }

            byte[] hash = provider.ComputeHash(Encoding.Unicode.GetBytes(data));

            byte[] toGuid = new byte[16];
            Array.Copy(hash, toGuid, 16);

            return new Guid(toGuid);
        }
    }
}