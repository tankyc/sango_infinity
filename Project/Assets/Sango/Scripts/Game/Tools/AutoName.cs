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
    /// 名按性别分为男名（MaleGivingName）和女名（FemaleGivingName），
    /// 可混用的名会同时出现在男名和女名两个集合中。
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
            /// <summary>男名集合</summary>
            public List<string> MaleGivingName = new List<string>();
            /// <summary>女名集合</summary>
            public List<string> FemaleGivingName = new List<string>();
        }

        /// <summary>合并后的姓集合</summary>
        private List<string> mFirstNameList = new List<string>();
        /// <summary>合并后的男名集合</summary>
        private List<string> mMaleGivingNameList = new List<string>();
        /// <summary>合并后的女名集合</summary>
        private List<string> mFemaleGivingNameList = new List<string>();

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
            mMaleGivingNameList.Clear();
            mFemaleGivingNameList.Clear();

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
                if (config.MaleGivingName != null && config.MaleGivingName.Count > 0)
                {
                    mMaleGivingNameList.AddRange(config.MaleGivingName);
                }
                if (config.FemaleGivingName != null && config.FemaleGivingName.Count > 0)
                {
                    mFemaleGivingNameList.AddRange(config.FemaleGivingName);
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
        /// 获取随机名（不分性别，从男名和女名合并的集合中随机）。
        /// </summary>
        public string GetRandomGivingName()
        {
            EnsureInit();
            List<string> all = new List<string>(mMaleGivingNameList);
            all.AddRange(mFemaleGivingNameList);
            if (all.Count == 0)
                return "";
            return all[Random.Range(0, all.Count)];
        }

        /// <summary>
        /// 获取随机名。
        /// </summary>
        /// <param name="isMale">true 表示从男名中随机，false 表示从女名中随机</param>
        public string GetRandomGivingName(bool isMale)
        {
            EnsureInit();
            List<string> list = isMale ? mMaleGivingNameList : mFemaleGivingNameList;
            if (list == null || list.Count == 0)
            {
                // 回退到另一性别的名集合，避免返回空名
                List<string> other = isMale ? mFemaleGivingNameList : mMaleGivingNameList;
                list = other;
            }
            if (list == null || list.Count == 0)
                return "";
            return list[Random.Range(0, list.Count)];
        }

        /// <summary>
        /// 获取一个随机姓名（姓 + 名，不分性别）。
        /// </summary>
        public string GetRandomName()
        {
            return GetRandomFirstName() + GetRandomGivingName();
        }

        /// <summary>
        /// 获取一个随机姓名（姓 + 名，按性别选择名）。
        /// </summary>
        /// <param name="isMale">true 使用男名，false 使用女名</param>
        public string GetRandomName(bool isMale)
        {
            return GetRandomFirstName() + GetRandomGivingName(isMale);
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
        /// 获取一个随机姓名，并通过 out 参数返回姓和名（按性别选择名）。
        /// </summary>
        public string GetRandomName(bool isMale, out string firstName, out string givingName)
        {
            firstName = GetRandomFirstName();
            givingName = GetRandomGivingName(isMale);
            return firstName + givingName;
        }

        /// <summary>
        /// 姓的数量。
        /// </summary>
        public int FirstNameCount { get { return mFirstNameList.Count; } }

        /// <summary>
        /// 男名数量。
        /// </summary>
        public int MaleGivingNameCount { get { return mMaleGivingNameList.Count; } }

        /// <summary>
        /// 女名数量。
        /// </summary>
        public int FemaleGivingNameCount { get { return mFemaleGivingNameList.Count; } }

        /// <summary>
        /// 名总数（男名 + 女名）。
        /// </summary>
        public int GivingNameCount { get { return mMaleGivingNameList.Count + mFemaleGivingNameList.Count; } }

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
