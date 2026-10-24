using System.Collections.Generic;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    /// <summary>
    /// 外交界面
    /// </summary>
    public class UIDiplomacy : UGUIWindow
    {
        public Text windowTitle;
        public UIObjectList forceList;
        public UIObjectList diplomatList;
        public Text relationLabel;
        public Text relationStatusLabel;
        public Text diplomatInfoLabel;
        public Text successRateLabel;
        public Text actionResultLabel;
        public Button allianceButton;


        public Button truceButton;
        public Button declareWarButton;
        public Button sendGiftButton;

        // 请求技术 / 请求兵力 / 通商 / 和亲 / 请求结盟 / 请求停战 的按钮字段已删除：
        // 原版没有这些命令（`s11_sys_person_order_diplomacy.cpp` 只有 7 个），
        // 保留占位按钮只会让玩家点了没反应。prefab 上对应的对象也应一并移除。
        public Button cancelButton;

        private DiplomacySystem _diplomacySystem;
        private Force _selectedForce;
        private Person _selectedDiplomat;

        /// <summary>
        /// 显示外交界面
        /// </summary>
        /// <param name="objects">参数</param>
        public override void OnOpen(params object[] objects)
        {
            _diplomacySystem = objects[0] as DiplomacySystem;
            windowTitle.text = "外交";
            actionResultLabel.text = "";

            List<SangoObject> sangoObjects = new List<SangoObject>(_diplomacySystem.AllForces);
            // 初始化势力列表
            forceList.Init(sangoObjects, ForceSortFunction.SortByName, OnForceSelected);
            forceList.SelectDefaultObject(_diplomacySystem.TargetForce);

            // 初始化武将列表
            UpdateDiplomatList();
        }

        /// <summary>
        /// 选择势力
        /// </summary>
        /// <param name="index">势力索引</param>
        private void OnForceSelected(int index)
        {
            _selectedForce = _diplomacySystem.AllForces[index] as Force;
            UpdateRelationInfo();
            UpdateActionButtons();
        }

        /// <summary>
        /// 更新武将列表
        /// </summary>
        private void UpdateDiplomatList()
        {
            Force currentForce = Scenario.Cur.CurRunForce;
            if (currentForce == null)
                return;

            // 获取当前势力的所有空闲武将
            List<SangoObject> diplomats = new List<SangoObject>();
            currentForce.ForEachPerson(person =>
            {
                if (person.IsFree)
                {
                    diplomats.Add(person);
                }
            });

            // 初始化武将列表
            diplomatList.Init(diplomats, PersonSortFunction.SortByName, OnDiplomatSelected);
            
            // 默认选择第一个武将
            if (diplomats.Count > 0)
            {
                _selectedDiplomat = diplomats[0] as Person;
                UpdateDiplomatInfo();
            }
            else
            {
                _selectedDiplomat = null;
                diplomatInfoLabel.text = "无可用使者";
            }
        }

        /// <summary>
        /// 选择武将
        /// </summary>
        /// <param name="index">武将索引</param>
        private void OnDiplomatSelected(int index)
        {
            Force currentForce = Scenario.Cur.CurRunForce;
            if (currentForce == null)
                return;

            // 获取当前势力的所有空闲武将
            List<Person> diplomats = new List<Person>();
            currentForce.ForEachPerson(person =>
            {
                if (person.IsFree)
                {
                    diplomats.Add(person);
                }
            });

            if (index >= 0 && index < diplomats.Count)
            {
                _selectedDiplomat = diplomats[index];
                UpdateDiplomatInfo();
                UpdateActionButtons();
            }
        }

        /// <summary>
        /// 更新关系信息
        /// </summary>
        private void UpdateRelationInfo()
        {
            if (_selectedForce == null)
                return;

            Force currentForce = Scenario.Cur.CurRunForce;
            // 【对齐原版】原先显示 raw 关系值（-5000~5000），并用一套自造阈值分档
            // （-1000/-500/500/1000 → 敌对/紧张/中立/友好/亲密），
            // 与 `DiplomacyRules.GetYookouLevel` 的 20/40/60/80 完全不一致 ——
            // 同一势力在势力情报界面与这里会显示不同档位。
            // 现统一走 0~100 友好度 + 原版五档。
            int like = currentForce != null ? currentForce.GetLike(_selectedForce) : DiplomacyRules.DefaultLike;
            relationLabel.text = $"关系值: {like}";
            relationStatusLabel.text = $"关系状态: {DiplomacyRules.GetYookouName(like)}";
        }

        /// <summary>
        /// 更新武将信息
        /// </summary>
        private void UpdateDiplomatInfo()
        {
            if (_selectedDiplomat == null)
            {
                diplomatInfoLabel.text = "无可用使者";
                return;
            }

            // 计算外交能力
            int diplomacyAbility = _selectedDiplomat.Politics + _selectedDiplomat.Glamour / 2;
            diplomatInfoLabel.text = $"使者: {_selectedDiplomat.Name}\n外交能力: {diplomacyAbility}\n政治: {_selectedDiplomat.Politics}\n魅力: {_selectedDiplomat.Glamour}";
        }

        /// <summary>
        /// 更新行动按钮状态
        /// </summary>
        private void UpdateActionButtons()
        {
            if (_selectedForce == null || _selectedDiplomat == null)
            {
                SetAllButtonsInteractable(false);
                if (successRateLabel != null)
                {
                    successRateLabel.text = "";
                }
                return;
            }

            Force currentForce = Scenario.Cur.CurRunForce;
            if (currentForce == _selectedForce)
            {
                // 不能对自己执行外交行动
                SetAllButtonsInteractable(false);
                if (successRateLabel != null)
                {
                    successRateLabel.text = "";
                }
                return;
            }

            // 检查各种外交行动的可行性
            DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
            allianceButton.interactable = diplomacyManager.CanPerformDiplomacyAction(DiplomacyActionType.Alliance, currentForce, _selectedForce);
            truceButton.interactable = diplomacyManager.CanPerformDiplomacyAction(DiplomacyActionType.Truce, currentForce, _selectedForce);
            declareWarButton.interactable = diplomacyManager.CanPerformDiplomacyAction(DiplomacyActionType.DeclareWar, currentForce, _selectedForce);
            sendGiftButton.interactable = diplomacyManager.CanPerformDiplomacyAction(DiplomacyActionType.SendGift, currentForce, _selectedForce);
            // 请求技术 / 请求兵力 / 通商 / 和亲 / 请求结盟 / 请求停战：原版没有这些命令，
            // 对应的按钮与处理方法已整体删除（占位 UI 只会让玩家点了没反应）。
        }

        /// <summary>
        /// 更新成功率显示
        /// </summary>
        /// <param name="actionType">外交行动类型</param>
        public void UpdateSuccessRateDisplay(DiplomacyActionType actionType)
        {
            if (_selectedForce == null || _selectedDiplomat == null || successRateLabel == null)
            {
                successRateLabel.text = "";
                return;
            }

            Force currentForce = Scenario.Cur.CurRunForce;
            DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
            // 创建外交行为实例并计算成功率
            DiplomacyActionBase action = diplomacyManager.CreateDiplomacyAction(actionType, currentForce, _selectedForce, _selectedDiplomat);
            int successRate = diplomacyManager.CalculateDiplomacySuccessRate(action);
            successRateLabel.text = $"成功率: {successRate}%";
        }

        /// <summary>
        /// 设置所有按钮的交互状态
        /// </summary>
        /// <param name="interactable">是否可交互</param>
        private void SetAllButtonsInteractable(bool interactable)
        {
            allianceButton.interactable = interactable;
            truceButton.interactable = interactable;
            declareWarButton.interactable = interactable;
            sendGiftButton.interactable = interactable;
        }

        /// <summary>
        /// 执行结盟
        /// </summary>
        public void OnAllianceButton()
        {
            if (_selectedForce == null || _selectedDiplomat == null)
                return;

            UpdateSuccessRateDisplay(DiplomacyActionType.Alliance);
            bool success = _diplomacySystem.PerformDiplomacyAction(DiplomacyActionType.Alliance, _selectedForce, _selectedDiplomat);
            UpdateActionResult(success, "结盟");
        }

        /// <summary>
        /// 执行停战
        /// </summary>
        public void OnTruceButton()
        {
            if (_selectedForce == null || _selectedDiplomat == null)
                return;

            UpdateSuccessRateDisplay(DiplomacyActionType.Truce);
            bool success = _diplomacySystem.PerformDiplomacyAction(DiplomacyActionType.Truce, _selectedForce, _selectedDiplomat);
            UpdateActionResult(success, "停战");
        }

        /// <summary>
        /// 执行宣战
        /// </summary>
        public void OnDeclareWarButton()
        {
            if (_selectedForce == null || _selectedDiplomat == null)
                return;

            UpdateSuccessRateDisplay(DiplomacyActionType.DeclareWar);
            bool success = _diplomacySystem.PerformDiplomacyAction(DiplomacyActionType.DeclareWar, _selectedForce, _selectedDiplomat);
            UpdateActionResult(success, "宣战");
        }

        /// <summary>
        /// 执行送礼
        /// </summary>
        public void OnSendGiftButton()
        {
            if (_selectedForce == null || _selectedDiplomat == null)
                return;

            UpdateSuccessRateDisplay(DiplomacyActionType.SendGift);
            // 使用配置的送礼金额
            int giftAmount = Scenario.Cur.Variables.diplomacySendGiftAmount;
            bool success = _diplomacySystem.PerformDiplomacyAction(DiplomacyActionType.SendGift, _selectedForce, _selectedDiplomat, giftAmount);
            UpdateActionResult(success, "送礼");
        }

        /// <summary>
        /// 更新行动结果
        /// </summary>
        /// <param name="success">是否成功</param>
        /// <param name="actionName">行动名称</param>
        private void UpdateActionResult(bool success, string actionName)
        {
            if (success)
            {
                actionResultLabel.text = $"{actionName}成功！";
            }
            else
            {
                actionResultLabel.text = $"{actionName}失败！";
            }

            // 更新关系信息和按钮状态
            UpdateRelationInfo();
            UpdateActionButtons();
        }

        /// <summary>
        /// 取消
        /// </summary>
        public void OnCancelButton()
        {
            _diplomacySystem.Exit();
        }
    }
}