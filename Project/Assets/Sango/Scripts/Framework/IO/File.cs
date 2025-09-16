
/*
'*******************************************************************
'Tank Framework
'*******************************************************************
*/
using System;
using System.Collections;
using System.IO;
namespace Sango
{
    public static class File
    {
        /// <summary>
        /// 读取所有二进制
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static byte[] ReadAllBytes(string fileName)
        {
            return System.IO.File.ReadAllBytes(fileName);
        }

        /// <summary>
        /// 读取所有二进制
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static string ReadAllText(string fileName)
        {
            return System.IO.File.ReadAllText(fileName);
        }

        public static string [] ReadAllLines(string fileName)
        {
            return System.IO.File.ReadAllLines(fileName);
        }

        public static void WriteAllText(string fileName, string text)
        {
            System.IO.File.WriteAllText(fileName, text);
        }

        public static void WriteAllBytes(string fileName,  byte[] bs)
        {
            System.IO.File.WriteAllBytes(fileName, bs);
        }

        /// <summary>
        /// 文件是否存在
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static bool Exists(string fileName)
        {
            return System.IO.File.Exists(fileName);
        }

        /// <summary>
        /// 移动文件
        /// </summary>
        /// <param name="path"></param>
        /// <param name="dest"></param>
        /// <returns></returns>
        static public void Move(string path, string dest)
        {
            File.Delete(dest);
            Directory.Create(dest, false);
            System.IO.File.Move(path, dest);
        }

        /// <summary>
        /// 移动文件
        /// </summary>
        /// <param name="path"></param>
        /// <param name="dest"></param>
        /// <returns></returns>
        static public void Copy(string path, string dest)
        {
            Directory.Create(dest, false);
            System.IO.File.Copy(path, dest, true);
        }

        /// <summary>
        /// 删除文件
        /// </summary>
        /// <param name="path"></param>
        /// <param name="dest"></param>
        /// <returns></returns>
        static public void Delete(string path)
        {
            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);
        }

        static public string GetFileName(string path)
        {
            return System.IO.Path.GetFileName(path);
        }

        static public string GetFileNameWithoutExtension(string path)
        {
            return System.IO.Path.GetFileNameWithoutExtension(path);
        }

        static public string GetFileExtension(string path)
        {
            return System.IO.Path.GetExtension(path);
        }

        
    }
}
