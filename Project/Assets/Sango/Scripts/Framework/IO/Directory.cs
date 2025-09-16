/*
'*******************************************************************
'Tank Framework
'*******************************************************************
*/
using System;
using System.Collections;
using System.IO;
using UnityEngine.SocialPlatforms;
using UnityEngine.Windows;

namespace Sango
{
    public static class Directory
    {
        /// <summary>
        /// 创建文件夹
        /// </summary>
        /// <param name="path"></param>
        /// <param name="isDir"></param>
        static public void Create(string path, bool isDir = true)
        {
            if (isDir)
            {
                if (!System.IO.Directory.Exists(path))
                    System.IO.Directory.CreateDirectory(path);
            }
            else
            {
                string dir = System.IO.Path.GetDirectoryName(path);
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);
            }
        }

        /// <summary>
        /// 文件夹是否存在
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool Exists(string path)
        {
            return System.IO.Directory.Exists(path);
        }

        /// <summary>
        /// 移动文件
        /// </summary>
        /// <param name="path"></param>
        /// <param name="dest"></param>
        /// <returns></returns>
        static public void Move(string path, string dest)
        {
            System.IO.Directory.Move(path, dest);
        }

        static private string[] ReplacePath(string[] paths)
        {
            if (paths == null) return null;
            for (int i = 0; i < paths.Length; i++)
            {
                string dir = paths[i];
                paths[i] = dir.Replace("\\", "/");
            }
            return paths;
        }

        /// <summary>
        /// 获取所有子文件夹
        /// </summary>
        /// <param name="path"></param>
        /// <param name="searchPattern"></param>
        /// <returns></returns>
        static public string[] GetDirectories(string path)
        {
            return ReplacePath(System.IO.Directory.GetDirectories(path));
        }

        static public string[] GetDirectories(string path, string searchPattern)
        {
            return ReplacePath(System.IO.Directory.GetDirectories(path, searchPattern));
        }

        static public string[] GetDirectories(string path, string searchPattern, SearchOption searchOption)
        {
            return ReplacePath(System.IO.Directory.GetDirectories(path, searchPattern, searchOption));
        }

        static public string GetDirectoryName(string path)
        {
            return System.IO.Path.GetFileNameWithoutExtension(path);
        }

        /// <summary>
        /// 获取文件夹里所有的文件
        /// </summary>
        /// <param name="path"></param>
        /// <param name="searchPattern"></param>
        /// <param name="searchOption"></param>
        /// <returns></returns>
        public static string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
        {
            return ReplacePath(System.IO.Directory.GetFiles(path, searchPattern, searchOption));
        }

        /// <summary>
        /// 删除文件
        /// </summary>
        /// <param name="path"></param>
        /// <param name="dest"></param>
        /// <returns></returns>
        static public void Delete(string path)
        {
            if (System.IO.Directory.Exists(path))
                System.IO.Directory.Delete(path, true);
        }

        static public void EnumFiles(string path, Action<string> action)
        {
            EnumFiles(path, "*", SearchOption.AllDirectories, action);
        }

        static public void EnumFiles(string path, string searchPattern, SearchOption searchOption, Action<string> action)
        {
            if (!Directory.Exists(path))
                return;

            string[] files = GetFiles(path, searchPattern, searchOption);
            foreach (string file in files)
            {
                action(file);
            }
        }
        static public void EnumDirectories(string path, string searchPattern, SearchOption searchOption, Action<string> action)
        {
            if (!Directory.Exists(path))
                return;

            string[] files = GetDirectories(path, searchPattern, searchOption);
            foreach (string file in files)
            {
                action(file);
            }
        }
    }
}
