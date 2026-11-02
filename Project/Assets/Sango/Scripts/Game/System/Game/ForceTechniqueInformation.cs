using Sango.Core.Player;
using Sango.UI;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 势力技巧情报：左侧列出全部存活势力，右侧只读展示被选中势力的技巧树。
    ///
    /// <para>
    /// 与 <see cref="ForceInformation"/>（势力情报）是同一套「列表 + 详情」范式：
    /// 同样遵循 Push 打开 / Back 返回 / OnExit 隐藏 / OnDestroy 关闭 的进出栈约定。
    /// 这里必须与之一致，否则会出现"返回后窗口不显示"或"关窗后残留"。
    /// </para>
    ///
    /// <para>
    /// 与「技巧研究」界面（<see cref="UITechnique"/>）的区别：
    /// 那个界面是**本城研发**，只显示自己势力的科技，并且可以选研究队伍、看花费；
    /// 本系统是**全势力查看**，纯只读，因此不涉及城池、费用与队伍。
    /// </para>
    ///
    /// <para>
    /// 【原版无对应实现】Open311（D:\open311\s11）只在 s11_enum_preset.h 里留了
    /// <c>CampaignId_Techniques</c> 枚举，技巧表界面的绘制与交互全在 san11pk.exe 内部，
    /// 没有可对齐的源码。因此本界面沿用项目既有的情报窗口范式自研：
    /// 列表数据一律取 <c>Force.IsAlive</c> 过滤后的 <c>Scenario.forceSet</c>
    /// （与 ForceInformation 的取法完全一致），不做额外筛选。
    /// </para>
    ///
    /// 【入口】
    /// · 势力情报窗口右下角「技巧表」按钮 → <see cref="UIForceInformation.OnTechniqueButton"/>；
    /// · 也可由外部系统直接调用 <see cref="Start(Force)"/>。
    /// </summary>
    [GameSystem]
    public class ForceTechniqueInformation : GameSystem
    {
        /// <summary>当前查看的势力</summary>
        public Force Target;

        /// <summary>左侧列表的数据源（全部存活势力）</summary>
        public List<SangoObject> all_objects = new List<SangoObject>();

        /// <summary>窗口预制体名</summary>
        protected string windowName = "window_information_force_techique";

        /// <summary>
        /// 打开技巧情报。
        /// 列表每次打开时重建，保证能反映"本回合是否有势力灭亡"。
        /// </summary>
        /// <param name="target">默认选中的势力；为 null 或已灭亡时退回当前行动势力</param>
        public void Start(Force target)
        {
            Scenario scenario = Scenario.Cur;
            List<SangoObject> obj_list = new List<SangoObject>();
            if (scenario != null && scenario.forceSet != null)
            {
                scenario.forceSet.ForEach(x =>
                {
                    if (x.IsAlive)
                        obj_list.Add(x);
                });
            }

            // 目标势力必须来自列表，否则左侧会选不中它（UIObjectList.SelectDefaultObject 找不到就停在 0）
            if (target == null || !target.IsAlive)
                target = obj_list.Count > 0 ? obj_list[0] as Force
                    : (scenario != null ? scenario.CurRunForce : null);

            Target = target;
            all_objects = obj_list;
            Push();
        }

        /// <summary>注册系统名（窗口标题取自这里）</summary>
        public override void Init()
        {
            Name = "技巧表";
        }

        /// <summary>进入系统：打开窗口</summary>
        public override void OnEnter()
        {
            Window.Instance.Open(windowName, this);
        }

        /// <summary>从子界面返回：重新显示窗口</summary>
        public override void OnBack(ICommandEvent whoGone)
        {
            Window.Instance.SetVisible(windowName, true);
        }

        /// <summary>离开系统：隐藏窗口（保留实例，窗口池复用）</summary>
        public override void OnExit()
        {
            Window.Instance.SetVisible(windowName, false);
        }

        /// <summary>系统销毁：真正关闭窗口</summary>
        public override void OnDestroy()
        {
            Window.Instance.Close(windowName);
        }

        /// <summary>取消 / 右键：返回上一层</summary>
        public override void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
        {
            switch (eventType)
            {
                case CommandEventType.Cancel:
                case CommandEventType.RClick:
                    {
                        Back();
                        break;
                    }
            }
        }
    }
}
