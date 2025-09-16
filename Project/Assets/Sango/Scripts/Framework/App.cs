/*
'*******************************************************************
'Tank Framework
'*******************************************************************
*/
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
namespace Sango
{
    /// <summary>
    /// Unity3D游戏主框架类
    /// </summary>
    public abstract class App<T> where T : class, new()
    {
        private static T _instance;
        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
#if UNITY_EDITOR
                    try
                    {
                        _instance = new T();
                    }
                    catch (System.Exception e)
                    {
                        UnityEngine.Debug.LogError(e);
                    }
#else
                    _instance = new T();
#endif
                }
                return _instance;
            }
        }

        /// <summary>
        /// 框架根游戏物体，脚本依赖此根，通常会挂到此游戏物体下
        /// </summary>
        internal MonoBehaviour rootBehaviour;
        /// <summary>
        /// 框架根行为脚本
        /// </summary>
        internal GameObject rootGameObject;
        /// <summary>
        /// 更新对象
        /// </summary>
        protected List<IUpdate> tickers = new List<IUpdate>(1024);

        public virtual void AddTick(IUpdate update)
        {
            tickers.Add(update);
        }

        public virtual void RemoveTick(IUpdate update)
        {
            tickers.Remove(update);
        }

        public virtual void Init(MonoBehaviour start, Platform.PlatformName targetPlatform)
        {
            rootBehaviour = start;
            rootGameObject = start.gameObject;

            Path.Init();
            Platform.targetPlatform = targetPlatform;
            Platform.Init();
            Scripts.Instance.Init();

        }

        public virtual void Update()
        {
            int count = tickers.Count;
            if (count == 0) return;
            float deltaTime = Time.deltaTime;
            float unscaledDeltaTime = Time.unscaledDeltaTime;
            for (int i = 0; i < count; ++i)
            {
                IUpdate update = tickers[i];
                if (!update.Update(deltaTime, unscaledDeltaTime))
                {
                    tickers.RemoveAt(i);
                    i--;
                }
            }
        }

        public virtual void Shutdown(){ }
        public virtual void Pause() { }
        public virtual void Resume() { }
        public Coroutine StartCoroutine(string methodName)
        {
            object value = null;
            return StartCoroutine(methodName, value);
        }
        public Coroutine StartCoroutine(string methodName, [DefaultValue("null")] object value)
        {
            return rootBehaviour.StartCoroutine(methodName, value);
        }
        public Coroutine StartCoroutine(IEnumerator routine)
        {
            return rootBehaviour.StartCoroutine(routine);
        }
        public void StopCoroutine(IEnumerator routine)
        {
            rootBehaviour.StopCoroutine(routine);
        }
        public void StopCoroutine(Coroutine routine)
        {
            rootBehaviour.StopCoroutine(routine);
        }
        public void StopCoroutine(string methodName)
        {
            rootBehaviour.StopCoroutine(methodName);
        }
        public void StopAllCoroutines()
        {
            rootBehaviour.StopAllCoroutines();
        }
    }
}