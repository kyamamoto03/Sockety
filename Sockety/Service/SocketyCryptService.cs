using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Sockety.Service
{
    internal class SocketyCryptService : IDisposable
    {

        RijndaelManaged rijndael = new RijndaelManaged();

        public SocketyCryptService(string AES_IV, string AES_Key)
        {
            rijndael.BlockSize = 128;
            rijndael.KeySize = 128;
            rijndael.Mode = CipherMode.CBC;
            rijndael.Padding = PaddingMode.PKCS7;

            rijndael.IV = Encoding.UTF8.GetBytes(AES_IV);
            rijndael.Key = Encoding.UTF8.GetBytes(AES_Key);

        }
        public byte[] Encrypt(byte[] text)
        {
            ICryptoTransform encryptor = rijndael.CreateEncryptor(rijndael.Key, rijndael.IV);

            byte[] encrypted;
            using (MemoryStream mStream = new MemoryStream())
            {
                using (CryptoStream ctStream = new CryptoStream(mStream, encryptor, CryptoStreamMode.Write))
                {
                    using (var sw = new BinaryWriter(ctStream))
                    {
                        sw.Write(text);
                    }
                    encrypted = mStream.ToArray();
                }
            }
            return encrypted;
        }

        /// <summary>
        /// 対称鍵暗号を使って暗号文を復号する
        /// </summary>
        /// <param name="cipher">暗号化された文字列</param>
        /// <param name="iv">対称アルゴリズムの初期ベクター</param>
        /// <param name="key">対称アルゴリズムの共有鍵</param>
        /// <returns>復号された文字列</returns>
        public byte[] Decrypt(byte[] cipher)
        {

            ICryptoTransform decryptor = rijndael.CreateDecryptor(rijndael.Key, rijndael.IV);

            byte[] plain = null;
            using (MemoryStream mStream = new MemoryStream(cipher))
            {
                using (CryptoStream ctStream = new CryptoStream(mStream, decryptor, CryptoStreamMode.Read))
                {
                    using (var a = new BinaryReader(ctStream))
                    {
                        plain = a.ReadBytes(cipher.Length);
                    }
                }
            }
            return plain;
        }

        public void Dispose()
        {
            rijndael.Dispose();
        }
    }
}
