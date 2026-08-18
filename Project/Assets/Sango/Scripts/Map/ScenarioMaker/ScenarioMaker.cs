using Sango.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TKNewtonsoft.Json;
using UnityEngine;

namespace Sango.ScenarioMaker
{
    /// <summary>
    /// 剧本编辑器核心管理类
    /// 负责剧本的新建、加载、保存以及基础武将数据的初始化
    /// </summary>
    public class ScenarioMaker : Singleton<ScenarioMaker>
    {
        /// <summary>
        /// 当前正在编辑的剧本
        /// </summary>
        public Scenario Scenario { get; private set; }

        /// <summary>
        /// 初始化剧本编辑器
        /// </summary>
        public void Init()
        {
            if (Sango.Path.ContentRootPath == null)
            {
                Sango.Path.Init();
            }

            if (GameData.Instance.ScenarioCommonData == null)
            {
                GameData.Instance.Init();
            }
        }

        /// <summary>
        /// 新建一个空白剧本
        /// 武将数据会从基础武将表读取并清空所属关系
        /// </summary>
        public void NewScenario()
        {
            Init();
            Scenario = new Scenario();
            Scenario.Info = new ScenarioInfo
            {
                id = 1,
                name = "新剧本",
                tag = "new",
                description = "",
                year = 190,
                month = 1,
                day = 1,
                curForceId = 0,
                mapType = "Default",
                turnCount = 0,
                priority = 0,
                isSave = false,
                playerForceList = new int[0],
                dateTime = DateTime.Now.ToFileTime()
            };
            Scenario.CommonData = GameData.Instance.ScenarioCommonData;
            Scenario.Variables = new ScenarioVariables();
            Scenario.View = new ScenarioView();
            Scenario.Map = null;
            LoadBasePersons();
            Sango.Log.Info("新建剧本完成");
        }

        /// <summary>
        /// 从文件加载剧本
        /// </summary>
        /// <param name="path">剧本文件路径</param>
        public void LoadScenario(string path)
        {
            Init();
            Scenario = new Scenario(path);
            Scenario.CommonData = GameData.Instance.ScenarioCommonData;
            Scenario.LoadContent();
            Sango.Log.Info($"剧本已加载: {System.IO.Path.GetFileName(path)}");
        }

        /// <summary>
        /// 将当前剧本保存到指定路径
        /// </summary>
        /// <param name="path">保存路径</param>
        public void SaveScenario(string path)
        {
            if (Scenario == null)
            {
                Sango.Log.Warning("当前没有可保存的剧本");
                return;
            }
            if (string.IsNullOrEmpty(path))
            {
                Sango.Log.Warning("保存路径为空");
                return;
            }
            Scenario.FilePath = path;
            Scenario.Info.dateTime = DateTime.Now.ToFileTime();
            Scenario.Export(path);
            Sango.Log.Info($"剧本已保存: {System.IO.Path.GetFileName(path)}");
        }

        /// <summary>
        /// 绑定外部已经加载的剧本（用于地图编辑器导入）
        /// </summary>
        /// <param name="scenario">剧本实例</param>
        public void SetScenario(Scenario scenario)
        {
            Scenario = scenario;
            if (Scenario != null && Scenario.CommonData == null)
            {
                Scenario.CommonData = GameData.Instance.LoadCommonData();
            }
            Sango.Log.Info("剧本编辑器已绑定当前剧本");
        }

        /// <summary>
        /// 从基础武将数据表加载武将并清空所属关系
        /// </summary>
        private void LoadBasePersons()
        {
            string personsPath = Sango.Path.FindFile("Data/Common/Persons.json");
            if (string.IsNullOrEmpty(personsPath))
            {
                personsPath = Sango.Path.ContentRootPath + "/Data/Common/Persons.json";
            }
            if (!File.Exists(personsPath))
            {
                Sango.Log.Error($"未找到基础武将数据: {personsPath}");
                return;
            }

            PersonSetWrapper wrapper = JsonConvert.DeserializeObject<PersonSetWrapper>(File.ReadAllText(personsPath));
            if (wrapper == null || wrapper.personSet == null)
            {
                Sango.Log.Warning("基础武将数据解析为空");
                return;
            }

            Scenario.personSet = wrapper.personSet;
            foreach (Person person in Scenario.personSet)
            {
                if (person == null)
                {
                    continue;
                }
                person.mBelongForce = null;
                person.mBelongCorps = null;
                person.mBelongCity = null;
                person.mCurrentCity = null;
                person.mTroop = null;
            }

            ResolveDelayedReferences();
            Sango.Log.Info($"已加载基础武将数量: {Scenario.personSet.DataCount}");
        }

        /// <summary>
        /// 手动触发延迟引用解析，避免影响其他已订阅的剧本事件
        /// </summary>
        private void ResolveDelayedReferences()
        {
            InvokeDelaySetValue<Id2ObjConverter<SangoObject>>("DelaySetValue", "OnScenarioPrepare");
            InvokeDelaySetValue<XY2CellConverter>("DelaySetValue", "OnScenarioPrepare");
        }

        /// <summary>
        /// 通过反射调用延迟引用解析器的静态OnScenarioPrepare方法
        /// </summary>
        private void InvokeDelaySetValue<T>(string nestedTypeName, string methodName)
        {
            Type converterType = typeof(T);
            Type nestedType = converterType.GetNestedType(nestedTypeName, BindingFlags.Public | BindingFlags.NonPublic);
            if (nestedType == null)
            {
                return;
            }
            MethodInfo method = nestedType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null)
            {
                return;
            }
            method.Invoke(null, new object[] { Scenario });
        }

        /// <summary>
        /// 基础武将数据包装类
        /// </summary>
        private class PersonSetWrapper
        {
            [JsonProperty]
            public SangoObjectSet<Person> personSet = new SangoObjectSet<Person>();
        }
    }
}
