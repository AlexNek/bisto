using System.Security.Cryptography;
using System.Text;

namespace Bisto.Helpers
{
    public static class HashHelpers

    {
        public static string CalculateCompactHash(string input, int length = 16)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                return Convert.ToBase64String(hashBytes).Substring(0, length);
            }
        }

        public static string CalculateHash(byte[] data)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(data);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }

        public static async Task<string> CalculateHashAsync(Stream stream)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = await sha256.ComputeHashAsync(stream);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
