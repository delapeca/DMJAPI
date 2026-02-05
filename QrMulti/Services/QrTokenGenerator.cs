using System.Security.Cryptography;
using System.Text;

namespace XNDmjApi.QrMulti.Services
{
    /// <summary>
    /// Generador de tokens opacs (hex) per QR Multi.
    /// - 16 bytes => 32 hex chars
    /// </summary>
    public static class QrTokenGenerator
    {
        public static string NewTokenHex(int bytes = 16)
        {
            var data = RandomNumberGenerator.GetBytes(bytes);
            var sb = new StringBuilder(bytes * 2);
            foreach (var b in data) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
