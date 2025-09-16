/*
'*******************************************************************
'Tank Framework
'*******************************************************************
*/
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
namespace Sango
{
    public static class Utility
    {
        #region 高低位互换
        public static byte[] GetBytes(float s, bool asc)
        {
            int buf = (int)(s * 100);
            return GetBytes(buf, asc);
        }

        public static float GetFloat(byte[] buf, bool asc)
        {
            int i = GetInt(buf, asc);
            float s = (float)i;
            return s / 100;
        }

        public static byte[] GetBytes(short s, bool asc)
        {
            byte[] buf = new byte[2];
            if (asc)
            {
                for (int i = buf.Length - 1; i >= 0; i--)
                {
                    buf[i] = (byte)(s & 0x00ff);
                    s >>= 8;
                }
            }
            else
            {
                for (int i = 0; i < buf.Length; i++)
                {

                    buf[i] = (byte)(s & 0x00ff);
                    s >>= 8;
                }
            }
            return buf;
        }

        public static byte[] GetBytes(int s, bool asc)
        {
            byte[] buf = new byte[4];
            if (asc)
                for (int i = buf.Length - 1; i >= 0; i--)
                {
                    buf[i] = (byte)(s & 0x000000ff);
                    s >>= 8;
                }
            else
                for (int i = 0; i < buf.Length; i++)
                {
                    buf[i] = (byte)(s & 0x000000ff);
                    s >>= 8;
                }
            return buf;
        }

        public static byte[] GetBytes(long s, bool asc)
        {
            byte[] buf = new byte[8];
            if (asc)
                for (int i = buf.Length - 1; i >= 0; i--)
                {
                    buf[i] = (byte)(s & 0x00000000000000ff);
                    s >>= 8;
                }
            else
                for (int i = 0; i < buf.Length; i++)
                {
                    buf[i] = (byte)(s & 0x00000000000000ff);
                    s >>= 8;
                }
            return buf;
        }

        public static short GetShort(byte[] buf, bool asc)
        {
            int length = 2;
            short r = 0;
            if (!asc)
                for (int i = length - 1; i >= 0; i--)
                {
                    r <<= 8;
                    r |= (short)(buf[i] & 0x00ff);
                }
            else
                for (int i = 0; i < length; i++)
                {
                    r <<= 8;
                    r |= (short)(buf[i] & 0x00ff);
                }
            return r;
        }

        public static int GetInt(byte[] buf, bool asc)
        {
            int length = 4;
            int r = 0;
            if (!asc)
                for (int i = length - 1; i >= 0; i--)
                {
                    r <<= 8;
                    r |= (buf[i] & 0x000000ff);
                }
            else
                for (int i = 0; i < length; i++)
                {
                    r <<= 8;
                    r |= (buf[i] & 0x000000ff);
                }
            return r;
        }

        public static long GetLong(byte[] buf, bool asc)
        {
            int length = 8;
            long r = 0;
            if (!asc)
                for (int i = length - 1; i >= 0; i--)
                {
                    r <<= 8;
                    r = (buf[i] & 0x00000000000000ff);
                }
            else
                for (int i = 0; i < length; i++)
                {
                    r <<= 8;
                    r |= ((long)buf[i] & 0x00000000000000ff);
                }
            return r;
        }
        #endregion

        #region MD5
        ///   <summary>
        ///   给一个字符串进行MD5加密
        ///   </summary>
        ///   <param   name="strText">待加密字符串</param>
        ///   <returns>加密后的字符串</returns>
        public static string MD5Encrypt(string strText)
        {

            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] result = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(strText));

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < result.Length; i++)
            {
                sb.Append(result[i].ToString("x2"));
            }

            if (Config.isDebug) Debug.Log("MD5Encrypt -> src: " + strText + " => " + sb.ToString());

            return sb.ToString();
        }

        /// <summary>
        /// 获取文件的MD5码
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        static public string GetMD5HashFromFile(string fileName)
        {
            try
            {
                FileStream file = new FileStream(fileName, FileMode.Open);
                System.Security.Cryptography.MD5 md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
                byte[] retVal = md5.ComputeHash(file);
                file.Close();

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < retVal.Length; i++)
                {
                    sb.Append(retVal[i].ToString("x2"));
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                throw new Exception("GetMD5HashFromFile() fail,error:" + ex.Message);
            }

        }
        #endregion
        /// <summary>
        /// 对字符串进行AES加密
        /// </summary>
        /// <param name="toEncrypt"></param>
        /// <returns></returns>
        public static string Encrypt_128_ECB(string toEncrypt)
        {
            RijndaelManaged rDel = new RijndaelManaged();
            return Encrypt_128_ECB(toEncrypt, rDel.Key);
        }
        public static string Encrypt_128_ECB(string toEncrypt,byte[] key)
        {
            string result;
            byte[] toEncryptArray = UTF8Encoding.UTF8.GetBytes(toEncrypt);
            RijndaelManaged rDel = new RijndaelManaged();
            rDel.Key = key;
            rDel.Mode = CipherMode.ECB;
            rDel.Padding = PaddingMode.PKCS7;
            ICryptoTransform cTransform = rDel.CreateEncryptor();
            byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
            result = Convert.ToBase64String(resultArray, 0, resultArray.Length);
            Debug.Log("data = " + result);
            return result;
        }
    }
}