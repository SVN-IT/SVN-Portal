using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.Utils
{
    public class SVNSecurity
    {
        public static string GetSalt()
        {
            byte[] array = new byte[16];
            using RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create();
            randomNumberGenerator.GetBytes(array);
            return BitConverter.ToString(array).Replace("-", "").ToLower();
        }

        public static string GetHash(string text)
        {
            using SHA256 sHA = SHA256.Create();
            byte[] array = sHA.ComputeHash(Encoding.UTF8.GetBytes(text));
            return BitConverter.ToString(array).Replace("-", "").ToLower();
        }
    }
}
