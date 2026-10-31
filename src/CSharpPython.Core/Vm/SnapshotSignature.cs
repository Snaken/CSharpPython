using System;
using System.Security.Cryptography;

namespace CSharpPython
{
    // The optional signature on a snapshot (VmOptions.SnapshotKey): HMAC-SHA256 of the snapshot bytes, written in front of them.
    internal static class SnapshotSignature
    {
        private const int MacLength = 32;

        /// <summary>The snapshot with its signature in front, or the snapshot itself when there is no key.</summary>
        public static byte[] Sign(byte[] key, byte[] snapshot)
        {
            if (key == null) return snapshot;
            using (var hmac = new HMACSHA256(key))
            {
                byte[] mac = hmac.ComputeHash(snapshot);
                var signed = new byte[MacLength + snapshot.Length];
                Buffer.BlockCopy(mac, 0, signed, 0, MacLength);
                Buffer.BlockCopy(snapshot, 0, signed, MacLength, snapshot.Length);
                return signed;
            }
        }

        /// <summary>The snapshot bytes after checking the signature, or the data unchanged when there is no key. Throws <see cref="SnapshotException"/> when the signature is missing or wrong.</summary>
        public static byte[] Verify(byte[] key, byte[] data)
        {
            if (key == null) return data;
            if (data.Length < MacLength) throw new SnapshotException("the snapshot was not signed with this key or has been changed");
            var snapshot = new byte[data.Length - MacLength];
            Buffer.BlockCopy(data, MacLength, snapshot, 0, snapshot.Length);
            var expected = new byte[MacLength];
            Buffer.BlockCopy(data, 0, expected, 0, MacLength);
            using (var hmac = new HMACSHA256(key))
            {
                if (!CryptographicOperations.FixedTimeEquals(hmac.ComputeHash(snapshot), expected))
                    throw new SnapshotException("the snapshot was not signed with this key or has been changed");
            }
            return snapshot;
        }
    }
}
