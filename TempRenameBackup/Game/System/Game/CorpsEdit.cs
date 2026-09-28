using Sango.Core.Player;
using Sango.UI;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 军团编辑系统 - 打开军团编辑窗口(window_edit_corps)
    /// 用于编辑军团信息: 军团长、军团城池、军团编号
    /// 关联窗口: window_edit_corps
    /// </summary>
    [GameSystem]
    public class CorpsEdit : GameSystem
    {
        /// <summary>
        /// 目标军团
        /// </summary>
        public Corps Target { get; private set; }

        /// <summary>
        /// 窗口名称 - 军团编辑窗口
        /// </summary>
        protected string windowName = "window_edit_corps";

        /// <summary>
        /// 启动军团编辑 - 传入目标军团并进入编辑状态
        /// </summary>
        /// <param name="target">需要编辑的军团</param>
        public void Start(Corps target)
        {
            Target = target;
            Push();
        }

        /// <summary>
        /// 初始化系统
        /// </summary>
        public override void Init()
        {
            Name = "军团编辑";
        }

        /// <summary>
        /// 进入编辑状态 - 打开军团编辑窗口
        /// </summary>
        public override void OnEnter()
        {
            Window.Instance.Open(windowName, Target);
        }

        /// <summary>
        /// 子命令返回时恢复窗口可见
        /// </summary>
        /// <param name="whoGone">返回的命令</param>
        public override void OnBack(ICommandEvent whoGone)
        {
            Window.Instance.SetVisible(windowName, true);
        }

        /// <summary>
        /// 退出编辑状态 - 隐藏军团编辑窗口
        /// </summary>
        public override void OnExit()
        {
            Window.Instance.SetVisible(windowName, false);
        }

        /// <summary>
        /// 销毁系统 - 关闭军团编辑窗口
        /// </summary>
        public override void OnDestroy()
        {
            Window.Instance.Close(windowName);
        }

        /// <summary>
        /// 处理输入事件 - 取消/右键返回
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="cell">格子</param>
        /// <param name="clickPosition">点击位置</param>
        /// <param name="isOverUI">是否在UI上</param>
        public override void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
        {
            if (eventType == CommandEventType.Cancel || eventType == CommandEventType.RClick)
            {
                Back();
            }
        }

        /// <summary>
        /// 设置军团长 - 军团长必须是本势力的武将
        /// </summary>
        /// <param name="comander">新的军团长,可为null(无军团长)</param>
        public void SetComander(Person comander)
        {
            if (Target == null)
            {
                return;
            }
            if (comander != null && comander.mBelongForce != Target.mBelongForce)
            {
                Log.Warning("军团长必须是本势力的武将");
                return;
            }
            Target.Comander = comander != null ? comander.Id : 0;
            Target.mComander = comander;
            Log.Info("军团 " + Target.Name + " 的军团长已设置为 " + (comander != null ? comander.Name : "无"));
        }

        /// <summary>
        /// 设置军团编号 - 范围为1-8
        /// </summary>
        /// <param name="number">新的军团编号</param>
        public void SetNumber(int number)
        {
            if (Target == null)
            {
                return;
            }
            if (number < 1 || number > 8)
            {
                Log.Warning("军团编号范围: 1-8");
                return;
            }
            Target.number = number;
            Log.Info("军团编号已设置为 " + number);
        }

        /// <summary>
        /// 将本势力的城市分配给军团
        /// </summary>
        /// <param name="city">要分配的城市</param>
        public void AssignCity(City city)
        {
            if (Target == null || city == null)
            {
                return;
            }
            if (city.mBelongForce != Target.mBelongForce)
            {
                Log.Warning("只能将本势力的城市分配给军团");
                return;
            }
            city.BelongCorps = Target.Id;
            city.mBelongCorps = Target;
            Log.Info("城市 " + city.Name + " 已加入军团 " + Target.Name);
        }

        /// <summary>
        /// 将城市从军团中移除 - 城市脱离军团但仍属于势力
        /// </summary>
        /// <param name="city">要移除的城市</param>
        public void RemoveCity(City city)
        {
            if (Target == null || city == null)
            {
                return;
            }
            if (city.mBelongCorps != Target)
            {
                return;
            }
            city.BelongCorps = 0;
            city.mBelongCorps = null;
            Log.Info("城市 " + city.Name + " 已脱离军团 " + Target.Name);
        }
    }
}
