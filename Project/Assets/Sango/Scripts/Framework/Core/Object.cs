/*
'*******************************************************************
'Tank Framework
'*******************************************************************
*/
using System;
using System.Collections;
using System.Collections.Generic;
namespace Sango
{
    /// <summary>
    /// 模块对象, 
    /// 这是XEngine的核心基类, 所有对象都应该从该类派生, 
    /// 定义了一整套事件处理机制,事件响应都以on开头
    /// 生命周期从注册开始至到注销,否则将一直存在于内存中
    /// </summary>
    public class Object : IObject
    {

        #region ManagerDATA
        static private Hashtable sObjectPool = new Hashtable();
        static private List<IObject> sObjectList = new List<IObject>();
        static private bool sHasRemoved = false;
        static private long sObjectID = 0;

        /// <summary>
        /// 获取一个类型为T的模块,如果没有则尝试创建该模块并注册名字为name
        /// 简单单件工厂类
        /// </summary>
        /// <typeparam name="T">获取类型</typeparam>
        /// <param name="name">尝试注册的模块名字, 可能会因为名字问题导致注册失败</param>
        /// <returns>返回获取到的模块或者创建并注册成功的模块, 没有获取到或尝试创建失败则返回null</returns>
        static public T GetObject<T>(string name) where T : new()
        {
            IObject returnValue = sObjectPool[name] as IObject;
            if (returnValue != null)
                return (T)returnValue;

            T t = new T();
            returnValue = t as IObject;
            if (returnValue != null)
            {
                _Register(name, returnValue);
                return t;
            }
            return default(T);
        }

        /// <summary>
        /// 获取一个模块,不会尝试创建
        /// </summary>
        /// <param name="name">模块名字</param>
        /// <returns>返回获取到的模块, 没有获取到则返回null</returns>
        static public IObject GetObject(string name)
        {
            return sObjectPool[name] as IObject;
        }


        /// <summary>
        /// 注册模块
        /// </summary>
        /// <param name="name">模块注册名字, 该名字将会成为此模块唯一的代号</param>
        /// <param name="Module">需要注册的模块</param>
        /// <returns>是否注册成功</returns>
        static protected bool _Register(string name, IObject obj)
        {
            if (string.IsNullOrEmpty(name))
                name = (sObjectID++).ToString();

            IObject returnValue = sObjectPool[name] as IObject;
            if (returnValue != null)
            {
                Log.Error(name + "名字重复,已经存在同名Module!", Log.LogType.Object);
                return false;
            }

            if (!obj.OnRegister(name))
            {
                Log.Error(name + "已经注册,不能重复注册!", Log.LogType.Object);
                return false;
            }

            sObjectPool[name] = obj;
            sObjectList.Add(obj);

            if (Config.isDebug)
            {
                Log.Print("!!!!!!!!! Module : " + name + "注册成功!!", Log.LogType.Object);
            }
            return true;
        }

        /// <summary>
        /// 卸载模块
        /// </summary>
        /// <param name="name">将要卸载的模块名字</param>
        static protected void _Unregister(string name)
        {
            IObject temp = sObjectPool[name] as IObject;
            if (temp != null)
            {
                temp.OnUnregister();
                ((Object)temp).MarkRemove();
                sObjectPool.Remove(name);
                sHasRemoved = true;
                if (Config.isDebug)
                {
                    Log.Print("xxxxx 反注册 Module : " + name, Log.LogType.Object);
                }
            }
        }

        /// <summary>
        /// 卸载模块
        /// </summary>
        /// <param name="name">将要卸载的模块</param>
        static protected void Unregister(IObject obj)
        {
            _Unregister(obj.GetName());
        }

        /// <summary>
        /// 向某一个Module发送消息
        /// </summary>
        /// <param name="ModuleName">模块名字</param>
        /// <param name="gMsg">消息对象</param>
        static public void SendMessage(string objName, params object[] parms)
        {
            IObject obj = GetObject(objName);
            SendMessage(obj, parms);
        }

        /// <summary>
        /// 向某一个Module发送消息
        /// </summary>
        /// <param name="ModuleName">模块对象</param>
        /// <param name="gMsg">消息对象</param>
        static public void SendMessage(IObject obj, params object[] parms)
        {
            if (obj != null && !obj.IsRemoved()) obj.OnMessage(parms);
        }

        /// <summary>
        /// 重置所有Module
        /// </summary>
        static public void ResetAll()
        {
            // 通知各功能重置
            for (int i = 0; i < sObjectList.Count; ++i)
                sObjectList[i].OnReset();
        }

        /// <summary>
        /// 释放所有Module
        /// </summary>
        static public void ClearAll()
        {
            if (Config.isDebug)
                Log.Print("释放了 " + sObjectPool.Count + " 个Module", Log.LogType.Object);

            for (int i = 0; i < sObjectList.Count; ++i)
            {
                IObject obj = sObjectList[i];
                if (Config.isDebug)
                    Log.Print("释放 " + obj.GetName(), Log.LogType.Object);
                obj.DeleteObject();
            }

            sObjectPool.Clear();
            sObjectList.Clear();
        }

        /// <summary>
        /// 游戏更新
        /// </summary>
        /// <param name="deltaTime"></param>
        static public void Update(float deltaTime, float unscaleDtTime)
        {
            for (int i = 0; i < sObjectList.Count; ++i)
            {
                IObject obj = sObjectList[i];
                if (!obj.IsRemoved())
                {
                    try
                    {
                        obj.OnUpdate(deltaTime, unscaleDtTime);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, Log.LogType.Object);
                    }
                }
            }

            if (sHasRemoved)
                sObjectList.RemoveAll(x => x.IsRemoved());
        }

        /// <summary>
        /// 游戏更新
        /// </summary>
        /// <param name="deltaTime"></param>
        static public void LateUpdate(float deltaTime, float unscaleDtTime)
        {
            for (int i = 0; i < sObjectList.Count; ++i)
            {
                IObject obj = sObjectList[i];
                if (!obj.IsRemoved())
                {
                    //try
                    //{
                        obj.OnLateUpdate(deltaTime, unscaleDtTime);
                    //}
                    //catch (Exception e)
                    //{
                    //    XLog.LogError(e, XLog.LogType.Object);
                    //}
                }
            }
        }


        #endregion // ManagerDATA


        /// <summary>
        /// 名字
        /// </summary>
        protected string mName;

        /// <summary>
        /// 类型
        /// </summary>
        protected int mType;

        /// <summary>
        /// 是否已经移除
        /// </summary>
        /// <returns></returns>
        private bool mIsRemoved = true;



        #region IGameModule实现

        public Object()
        {
            mType = 0;
        }

        /// <summary>
        /// 获取模块名字
        /// </summary>
        /// <returns></returns>
        public string GetName()
        {
            return mName;
        }

        /// <summary>
        /// 获取模块类型
        /// </summary>
        /// <returns></returns>
        public int GetXType()
        {
            return mType;
        }

        /// <summary>
        /// 更新回调
        /// </summary>
        /// <param name="dtTime"></param>
        public virtual void OnUpdate(float dtTime, float unscaleDtTime) { }

        /// <summary>
        /// 后更新回调
        /// </summary>
        /// <param name="dtTime"></param>
        public virtual void OnLateUpdate(float dtTime, float unscaleDtTime) { }

        /// <summary>
        /// 向游戏模块容器注册,并赋予模块名字
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public virtual bool OnRegister(string name)
        {
            mIsRemoved = false;
            mName = name;
            return true;
        }

        /// <summary>
        /// 反注册,即移除出游戏模块容器
        /// </summary>
        public virtual void OnUnregister()
        {
            // 先标记已经移除
            MarkRemove();
        }

        /// <summary>
        /// 收到消息处理
        /// </summary>
        /// <param name="gMsg"></param>
        /// <returns></returns>
        public virtual void OnMessage(params object[] parms) { }

        /// <summary>
        /// 游戏结束,或者卸载的时候回调
        /// </summary>
        public virtual void DeleteObject()
        {
            OnDeleted();
            Unregister();
        }

        /// <summary>
        /// virtual function
        /// </summary>
        public virtual void OnDeleted() { }

        /// <summary>
        /// 是否已经移除
        /// </summary>
        /// <returns></returns>
        public bool IsRemoved()
        {
            return mIsRemoved;
        }

        /// <summary>
        /// 标记移除
        /// </summary>
        public void MarkRemove()
        {
            mIsRemoved = true;
        }

        /// <summary>
        /// 当游戏重置的时候,会回调此函数
        /// </summary>
        public virtual void OnReset() { }

        ///

        #endregion IGameModule实现

        /// <summary>
        /// 是否已经注册
        /// </summary>
        /// <returns></returns>
        public bool IsRegister()
        {
            return !mIsRemoved;
        }

        /// <summary>
        /// 注册
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool Register(string name)
        {
            return _Register(name, this);
        }

        /// <summary>
        /// 注册
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool Register()
        {
            return Register(null);
        }

        /// <summary>
        /// 反注册
        /// </summary>
        public void Unregister()
        {
            if (IsRegister()) Unregister(this);
        }

    }
}