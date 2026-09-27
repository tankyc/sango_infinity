using Sango.Core;
using Sango.Core.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>
    /// 城市计略指令窗口的逻辑类，被每个计略自己的 prefab 引用
    /// （流言 window_city_strategy_rumor、二虎竞食 window_city_strategy_two_tigers）。
    /// 上半区用若干行文本交代"目标（一至两个）/ 交情 / 抵达日数 / 资金 / 行动力"，中部显示使者头像与五维，
    /// 底部是"选择目标""选择使者""决定""取消"按钮。
    /// 目标框数量由指令系统的 TargetSlotCount 决定：流言的 prefab 里根本没有第二框，
    /// 相应字段为空，下面的绘制会自然跳过；二虎的 prefab 里两框上下等距排列。
    /// 按钮回调只转调指令系统，窗口自身不做任何业务判断，也不缓存选择结果。
    /// </summary>
    public class UICityStrategy : UGUIWindow
    {
        public Text windowTitle;

        public UITextField target;
        public UITextField target2;
        public UITextField relationship;
        public UITextField days;
        public UITextField gold;

        /// <summary>
        /// 第二个目标框的选择按钮节点。prefab 里挂在嵌套按钮 prefab 实例的根上，
        /// Button 组件是否就在这一层由 prefab 决定，因此取 RectTransform 再向下找，不依赖具体层级；
        /// 与 target2 成对出现，流言只需要一个目标时整组隐藏
        /// </summary>
        public RectTransform selectTargetButton2;

        public UIPersonItem personItems;
        public UIStatusItem statusItem;
        public UITextField action_value;

        public Button sureButton;

        /// <summary>
        /// 当前正在执行的计略指令，由 OnOpen 从命令栈顶解析
        /// </summary>
        CityStrategySystemBase currentSystem;

        /// <summary>
        /// 开窗：绑定当前指令并刷出首屏内容。
        /// 栈顶不是计略指令时（例如窗口被误开）直接留白，不去动任何按钮状态
        /// </summary>
        public override void OnOpen()
        {
            currentSystem = GameSystemManager.Instance.CurrentCommand as CityStrategySystemBase;
            if (currentSystem == null)
                return;

            windowTitle.text = currentSystem.customTitleName;
            UpdateContent();
        }

        /// <summary>
        /// 指令系统在目标或使者变更后调用 Refresh 时走这里，保证两种刷新入口口径一致
        /// </summary>
        public override void OnRefresh()
        {
            UpdateContent();
        }

        /// <summary>
        /// 重绘窗口内容：目标框（一至两个）、交情、抵达日数、行动力/资金余量、使者头像与五维、决定按钮可用性。
        /// 目标框数量与每个框的标题都由指令系统给出，窗口只负责按需显示第二框，
        /// 抵达日数直接取指令系统折算好的日数，避免在 UI 里另写一套距离口径
        /// </summary>
        public void UpdateContent()
        {
            if (currentSystem == null)
                return;

            City targetCity = currentSystem.TargetCity;
            Corps corps = targetCity != null ? targetCity.mBelongCorps : null;
            action_value.text = $"{currentSystem.JobActionPointCost}/{corps?.ActionPoint ?? 0}";
            gold.text = $"{currentSystem.JobGoldCost}/{targetCity?.gold ?? 0}";

            Person envoy = currentSystem.personList.Count > 0 ? currentSystem.personList[0] : null;
            personItems.SetPerson(envoy);
            statusItem.SetPerson(envoy);

            int slotCount = currentSystem.TargetSlotCount;
            target.SetTitle(currentSystem.GetTargetTitle(0));
            target.text = currentSystem.GetTargetDescription(0);

            // 第二目标框只有二虎竞食用到；流言的 prefab 里没有这个对象，字段为空即跳过，
            // 这里的显隐判断是留给"同一 prefab 里两框都在"的情况兜底
            bool hasSecondSlot = slotCount > 1;
            if (target2 != null)
            {
                target2.gameObject.SetActive(hasSecondSlot);
                target2.SetTitle(currentSystem.GetTargetTitle(1));
                target2.text = currentSystem.GetTargetDescription(1);
            }
            if (selectTargetButton2 != null)
            {
                selectTargetButton2.gameObject.SetActive(hasSecondSlot);
                Button secondButton = selectTargetButton2.GetComponentInChildren<Button>(true);
                // 第二框要先选定第一框才有"与谁相邻"的参照，没参照时置灰而不是藏起来，让玩家知道还差一步
                if (secondButton != null)
                    secondButton.interactable = hasSecondSlot && currentSystem.CanSelectTargetSlot(1);
            }

            relationship.text = currentSystem.RelationshipDescription;
            int distanceDays = currentSystem.EnvoyDistanceDays;
            days.text = distanceDays > 0 ? $"{distanceDays}日" : "";

            sureButton.interactable = envoy != null && currentSystem.TargetReady;
        }

        /// <summary>
        /// "决定"按钮：交给指令系统派遣
        /// </summary>
        public void OnSure()
        {
            currentSystem?.DoJob();
        }

        /// <summary>
        /// "取消"按钮：退出本指令
        /// </summary>
        public void OnCancel()
        {
            currentSystem?.Exit();
        }

        /// <summary>
        /// "选择使者"按钮：打开使者单选选择器，候选与初始勾选都由指令系统决定
        /// </summary>
        public void OnSelectPerson()
        {
            currentSystem?.OpenEnvoySelector();
        }

        /// <summary>
        /// "对象势力一/目标城池"按钮：打开第一个目标框的选择器
        /// </summary>
        public void OnSelectForce()
        {
            currentSystem?.OpenTargetSelector(0);
        }

        /// <summary>
        /// "对象势力二"按钮：打开第二个目标框的选择器（流言不显示本按钮）
        /// </summary>
        public void OnSelectForceSecond()
        {
            currentSystem?.OpenTargetSelector(1);
        }
    }
}
