using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace Tyvj.Migration.Lib
{
    public static class Crypto
    {
        private static MD5 _md5 = System.Security.Cryptography.MD5.Create();
        private static SHA1 _sha1 = System.Security.Cryptography.SHA1.Create();

        public const int SHA1Length = 40;
        public const int MD5Length = 32;

        public static string SHA1(string txt)
        {
            return string.Join("", _sha1.ComputeHash(Encoding.UTF8.GetBytes(txt)).Select(x => x.ToString("x2"))).ToLower();
        }

        public static string MD5(string txt)
        {
            return string.Join("", _md5.ComputeHash(Encoding.UTF8.GetBytes(txt)).Select(x => x.ToString("x2"))).ToLower();
        }
    }
}
