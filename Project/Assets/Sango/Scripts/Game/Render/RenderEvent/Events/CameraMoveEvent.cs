/*
 * 文件名：CameraMoveEvent.cs
 * 描述：相机移动事件类，用于处理相机移动和对话框显示
 * 创建日期：2026-03-27
 * 最后修改：2026-03-27
 */

using Sango.Core;
using Sango.UI;
using UnityEngine;

namespace Sango.Render
{
    /// <summary>
    /// 相机移动事件类，用于处理相机移动和对话框显示
    /// </summary>
    public class CameraMoveEvent : RenderEventBase
    {
        /// <summary>
        /// 目标位置
        /// </summary>
        public Vector3 targetPosition;

        /// <summary>
        /// 移动持续时间
        /// </summary>
        public float moveDuration = 0.5f;

        /// <summary>
        /// 起始位置
        /// </summary>
        private Vector3 startPosition;


        /// <summary>
        /// 对话框样式
        /// </summary>
        public GameDialog.DialogStyle dialogStyle;

        /// <summary>
        /// 对话框内容
        /// </summary>
        public string content;

        /// <summary>
        /// 人物对象
        /// </summary>
        public Person person;

        /// <summary>
        /// 确认按钮回调
        /// </summary>
        public System.Action sureAction;

        /// <summary>
        /// 取消按钮回调
        /// </summary>
        public System.Action cancelAction;

        /// <summary>
        /// 不返回镜头
        /// </summary>
        public bool donotReturn;

        float time = 0;

        /// <summary>
        /// 对话框实例
        /// </summary>
        GameDialog.IDialog dialog;

        /// <summary>
        /// 初始化方法
        /// </summary>
        /// <param name="targetPosition">目标位置</param>
        /// <param name="moveDuration">移动持续时间</param>
        /// <param name="dialogStyle">对话框样式</param>
        /// <param name="content">对话框内容</param>
        /// <param name="person">人物对象</param>
        /// <param name="sureAction">确认按钮回调</param>
        /// <param name="cancelAction">取消按钮回调</param>
        public CameraMoveEvent Init(Vector3 targetPosition, float moveDuration, GameDialog.DialogStyle dialogStyle, string content, Person person, System.Action sureAction, System.Action cancelAction)
        {
            this.targetPosition = targetPosition;
            this.moveDuration = moveDuration;
            this.dialogStyle = dialogStyle;
            this.content = content;
            this.person = person;
            this.sureAction = sureAction;
            this.cancelAction = cancelAction;
            IsDone = false;
            time = 0;
            return this;
        }

        public CameraMoveEvent Init(Vector3 targetPosition, float moveDuration)
        {
            this.targetPosition = targetPosition;
            this.moveDuration = moveDuration;
            donotReturn = true;
            time = 0;
            IsDone = false;
            return this;
        }

        /// <summary>
        /// 进入事件处理
        /// </summary>
        /// <param name="scenario">场景实例</param>
        public override void Enter(Scenario scenario)
        {
            base.Enter(scenario);
            GameController.Instance.KeyboardMoveEnabled = false;
            GameController.Instance.RotateViewEnabled = false;
            GameController.Instance.DragMoveViewEnabled = false;
            GameController.Instance.Enabled = false;
            startPosition = MapRender.Instance.GetCameraPos();
            MapRender.Instance.MoveCameraTo(targetPosition, moveDuration);
            if (!string.IsNullOrEmpty(content))
            {
                GameDialog.Instance.Open(dialogStyle, content,
               () =>
               {
                   sureAction?.Invoke();
                   IsDone = true;
               },
               () =>
               {
                   IsDone = true;
                   cancelAction?.Invoke();
               }, person);
            }
        }

        public override bool Update(Scenario scenario, float deltaTime)
        {
            if (string.IsNullOrEmpty(content))
            {
                time += deltaTime;
                if (time > moveDuration)
                    IsDone = true;
            }
            else
            {
                time += deltaTime;
                if (time > 3)
                    IsDone = true;
            }
            return IsDone;
        }

        /// <summary>
        /// 退出事件处理
        /// </summary>
        /// <param name="scenario">场景实例</param>
        public override void Exit(Scenario scenario)
        {
            GameController.Instance.KeyboardMoveEnabled = true;
            GameController.Instance.RotateViewEnabled = true;
            GameController.Instance.DragMoveViewEnabled = true;
            GameController.Instance.Enabled = true;
            if (!donotReturn)
                MapRender.Instance.MoveCameraTo(startPosition, moveDuration);
        }

    }
}