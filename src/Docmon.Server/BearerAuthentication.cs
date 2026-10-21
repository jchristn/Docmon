namespace Docmon.Server
{
    using System;
    using System.Security.Cryptography;
    using System.Text;
    /// <summary>Constant-time bearer credential validation shared by both transports.</summary>
    public sealed class BearerAuthentication
    {
        private readonly byte[] _Hash;
        /// <summary>Creates a verifier without retaining the plaintext token.</summary>
        public BearerAuthentication(string token) { _Hash = SHA256.HashData(Encoding.UTF8.GetBytes(token)); }
        /// <summary>Validates an Authorization header.</summary>
        /// <param name="header">Bearer authorization header.</param>
        /// <returns>True only when the token matches.</returns>
        public bool Validate(string? header)
        {
            if (header == null || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return false;
            byte[] candidate = SHA256.HashData(Encoding.UTF8.GetBytes(header.Substring(7)));
            return CryptographicOperations.FixedTimeEquals(_Hash, candidate);
        }
    }
}
