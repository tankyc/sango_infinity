using Sango.Core.Action;
using Sango.Core.Player;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 游戏模块基类,定义基本行为
    /// </summary>
    public class GameSystem : ICommandEvent
    {
        public string Name { get; protected set; }

        public static T GetSystem<T>(string name) where T : GameSystem
        {
            return GameSystemManager.Instance.GetSystem<T>(name);
        }

        public static T GetSystem<T>() where T : GameSystem
        {
            return GameSystemManager.Instance.GetSystem<T>();
        }

        public virtual void Push()
        {
            GameSystemManager.Instance.Push(this);
        }

        public virtual void Back()
        {
            GameSystemManager.Instance.Back(this);
        }

        /// <summary>
        /// 提供给外部获取的属性
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="pName"></param>
        /// <returns></returns>
        public virtual T GetProperty<T>(string pName) where T : struct
        {
            return default(T);
        }

        /// <summary>
        /// 提供给外部设置的属性
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="pName"></param>
        /// <param name="value"></param>
        public virtual void SetProperty<T>(string pName, T value) where T : struct
        {

        }

        public virtual void Enter()
        {
            GameSystemManager.Instance.Push(this);
        }

        public virtual void Exit()
        {
            GameSystemManager.Instance.Back(this);
        }

        public virtual void Done()
        {
            GameSystemManager.Instance.Done(this);
        }

        /// <summary>
        /// 进入当前命令的时候触发
        /// </summary>
        public virtual void OnEnter() {; }

        /// <summary>
        /// 离开当前命令的时候触发
        /// </summary>
        public virtual void OnExit() {; }

        /// <summary>
        /// 当前命令被重新拾起的时候触发(返回)
        /// </summary>
        public virtual void OnBack(ICommandEvent whoGone) {; }

        /// <summary>
        /// 当前命令被舍弃的时候触发
        /// </summary>
        public virtual void OnDestroy() {; }

        /// <summary>
        /// 结束整个命令链的时候触发
        /// </summary>
        public virtual void OnDone() { OnDestroy(); }

        /// <summary>
        /// 每帧更新
        /// </summary>
        public virtual void Update() {; }

        public virtual void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
        {
            switch (eventType)
            {
                case CommandEventType.Cancel:
                    Back(); break;
            }

        }

        public virtual void Init() { }
        public virtual void Clear() {; }

        /// <summary>
        /// 系统之间的消息处理
        /// </summary>
        /// <param name="message"></param>
        /// <param name="objects"></param>
        public virtual void OnMessage(string message, params object[] objects) {; }

    }
}
