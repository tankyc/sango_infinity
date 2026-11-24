using Sango.Core.Player;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    /// <summary>
    /// 剧本选择界面
    /// </summary>
    public class UITechniqueComplete : UGUIWindow
    {
        public UITechniqueItem techniqueItem;
        public Animation animation;

        public override void OnOpen(params object[] ps)
        {
            base.OnOpen();

            Technique technique = (ps != null && ps.Length > 0) ? ps[0] as Technique : null;
            if (technique == null)
            {
                // 数据缺失（典型来源：MOD 改动科技树后，旧存档正在研究的科技 Id 已不存在）。
                // 【不能在这里同步调用 Close()】WindowEvent.Enter 的时序是
                //     Window.Instance.Open(...)  → 触发本方法 → 然后才
                //     targetWindow.ugui_instance.OnCloseAction = OnWindowHide;
                // 同步 Close 会在 OnCloseAction 赋值之前就把窗口关掉，导致
                // RenderEvent 永远收不到完成回调、IsDone 恒为 false ——
                // 整条演出队列卡死（表现是游戏停在那里不动），比崩溃更难查。
                // 因此推迟到下一帧（此时 OnCloseAction 已就位）再关。
                Sango.Log.Warning("*研究完成演出缺少科技数据（可能被 MOD 移除），已跳过展示");
                animation.Play();
                Invoke("Close", animation.clip.length);
                return;
            }

            techniqueItem.SetTechnique(technique);
            animation.Play();
            Invoke("Close", animation.clip.length);
        }
    }
}
