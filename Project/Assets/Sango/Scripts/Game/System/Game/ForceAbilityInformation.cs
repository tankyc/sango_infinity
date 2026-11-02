using Sango.Core.Player;
using Sango.UI;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 势力能力情报：左侧列出全部存活势力，右侧只读展示被选中势力的能力路径图。
    ///
    /// <para>
    /// 与 <see cref="ForceTechniqueInformation"/>（技巧表）是同一套「列表 + 详情」范式：
    /// Push 打开 / Back 返回 / OnExit 隐藏 / OnDestroy 关闭，四个钩子缺一不可。
    /// </para>
    ///
    /// <para>
    /// 与「能力研究」界面（<see cref="UICityAbilityResearch"/>）的区别：
    /// 那个界面是**本城研发**，要读城池资金、行动力并允许下单；
    /// 本系统是**全势力查看**，纯只读，因此只带 Force 不涉及 City。
    /// 数据口径不重复实现：能力表读 <c>Scenario.CommonData.Abilities</c>，
    /// 可研究候选直接问 <see cref="AbilityStudySystem.GetResearchableAbilities"/>。
    /// </para>
    ///
    /// 【入口】势力情报窗口右下角「能力表」按钮 → <see cref="UIForceInformation.OnAabilityButton"/>。
    /// </summary>
    [GameSystem]
    public class ForceAbilityInformation : GameSystem
    {
        /// <summary>当前查看的势力</summary>
        public Force Target;

        /// <summary>左侧列表的数据源（全部存活势力）</summary>
        public List<SangoObject> all_objects = new List<SangoObject>();

        /// <summary>窗口预制体名</summary>
        protected string windowName = "window_information_force_ability";

        /// <summary>
        /// 打开能力情报。
        /// 列表每次打开时重建，保证能反映"本回合是否有势力灭亡"。
        /// </summary>
        /// <param name="target">默认选中的势力；为 null 或已灭亡时退回列表首个势力</param>
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

            // 目标势力必须来自列表，否则左侧选不中它（UIObjectList.SelectDefaultObject 找不到就停在 0）
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
            Name = "能力表";
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
