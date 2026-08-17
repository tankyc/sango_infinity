/*
'*******************************************************************
'Tank Framework
'*******************************************************************
*/
using Sango.Mod;
using System.Collections.Generic;
using UnityEngine;

namespace Sango
{
    /// <summary>
    /// 自动姓名工具类（单例）。
    /// 初始化时通过 Mod 加载所有 Content\Data 下的 NameConfig.json 并合并数据，
    /// 提供随机姓名的方法。
    /// </summary>
    public class AutoName : Singleton<AutoName>
    {
        /// <summary>
        /// NameConfig.json 的数据结构。
        /// </summary>
        public class NameConfig
        {
            /// <summary>姓的集合</summary>
            public List<string> FirstName = new List<string>();
            /// <summary>名的集合</summary>
            public List<string> GivingName = new List<string>();
        }

        /// <summary>合并后的姓集合</summary>
        private List<string> mFirstNameList = new List<string>();
        /// <summary>合并后的名集合</summary>
        private List<string> mGivingNameList = new List<string>();

        private bool mInited = false;

        /// <summary>
        /// 初始化：加载基础 Content\Data 下的 NameConfig.json，
        /// 再遍历所有 Mod 下的 NameConfig.json 进行数据合并。
        /// </summary>
        public void Init()
        {
            if (mInited)
                return;

            mFirstNameList.Clear();
            mGivingNameList.Clear();

            // 基础数据
            string baseFile = Path.ContentRootPath + "/Data/NameConfig.json";
            if (File.Exists(baseFile))
            {
                LoadAndMerge(baseFile);
            }

            // Mod 数据合并（Mod 在前，后加载的 Mod 数据追加到集合后面）
            ModManager.Instance.EnumFiles("Data/NameConfig.json", file =>
            {
                LoadAndMerge(file);
            });

            mInited = true;
        }

        /// <summary>
        /// 读取并合并单个 NameConfig.json 文件的数据。
        /// </summary>
        private void LoadAndMerge(string file)
        {
            if (string.IsNullOrEmpty(file) || !File.Exists(file))
                return;

            try
            {
                NameConfig config = new NameConfig();
                TKNewtonsoft.Json.JsonConvert.PopulateObject(File.ReadAllText(file), config);
                if (config.FirstName != null && config.FirstName.Count > 0)
                {
                    mFirstNameList.AddRange(config.FirstName);
                }
                if (config.GivingName != null && config.GivingName.Count > 0)
                {
                    mGivingNameList.AddRange(config.GivingName);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[AutoName] 加载 NameConfig.json 失败: " + file + "\n" + e);
            }
        }

        /// <summary>
        /// 获取随机姓。
        /// </summary>
        public string GetRandomFirstName()
        {
            EnsureInit();
            if (mFirstNameList.Count == 0)
                return "";
            return mFirstNameList[Random.Range(0, mFirstNameList.Count)];
        }

        /// <summary>
        /// 获取随机名。
        /// </summary>
        public string GetRandomGivingName()
        {
            EnsureInit();
            if (mGivingNameList.Count == 0)
                return "";
            return mGivingNameList[Random.Range(0, mGivingNameList.Count)];
        }

        /// <summary>
        /// 获取一个随机姓名（姓 + 名）。
        /// </summary>
        public string GetRandomName()
        {
            return GetRandomFirstName() + GetRandomGivingName();
        }

        /// <summary>
        /// 获取一个随机姓名，并通过 out 参数返回姓和名。
        /// </summary>
        public string GetRandomName(out string firstName, out string givingName)
        {
            firstName = GetRandomFirstName();
            givingName = GetRandomGivingName();
            return firstName + givingName;
        }

        /// <summary>
        /// 姓的数量。
        /// </summary>
        public int FirstNameCount { get { return mFirstNameList.Count; } }

        /// <summary>
        /// 名的数量。
        /// </summary>
        public int GivingNameCount { get { return mGivingNameList.Count; } }

        /// <summary>
        /// 确保已初始化。
        /// </summary>
        private void EnsureInit()
        {
            if (!mInited)
                Init();
        }
    }
}
