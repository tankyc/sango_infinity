using Sango.Core.Player;
using UnityEngine.UI;
using System.Collections.Generic;

using Sango.Core; namespace Sango.UI
{
    /// <summary>
    /// 军团菜单UI类
    /// </summary>
    public class UICorpsMenuSetting : UGUIWindow
    {
        /// <summary>
        /// 窗口标题
        /// </summary>
        public Text windowTitle;
        
        /// <summary>
        /// 编制都市按钮
        /// </summary>
        public Button organizeCityButton;
        
        /// <summary>
        /// 都市数量显示文本
        /// </summary>
        public Text cityCountText;
        
        /// <summary>
        /// 军团方针按钮
        /// </summary>
        public Button policyButton;
        
        /// <summary>
        /// 军团方针显示文本
        /// </summary>
        public Text policyText;
        
        /// <summary>
        /// 委任内容按钮
        /// </summary>
        public Button appointmentButton;
        
        /// <summary>
        /// 当前军团系统
        /// </summary>
        CorpsSystem currentSystem;
        
        /// <summary>
        /// 当前选中的军团
        /// </summary>
        Corps currentCorps;
        
        /// <summary>
        /// 窗口显示时调用
        /// </summary>
        public override void OnOpen()
        {
            currentSystem = GameSystem.GetSystem<CorpsSystem>();
            windowTitle.text = currentSystem.customTitleName;
            UpdateContent();
            
            // 添加按钮事件监听
            organizeCityButton.onClick.AddListener(OnOrganizeCityButtonClick);
            policyButton.onClick.AddListener(OnPolicyButtonClick);
            appointmentButton.onClick.AddListener(OnAppointmentButtonClick);
        }
        
        /// <summary>
        /// 窗口隐藏时调用
        /// </summary>
        public override void OnClose()
        {
            // 移除按钮事件监听
            organizeCityButton.onClick.RemoveListener(OnOrganizeCityButtonClick);
            policyButton.onClick.RemoveListener(OnPolicyButtonClick);
            appointmentButton.onClick.RemoveListener(OnAppointmentButtonClick);
        }
        
        /// <summary>
        /// 更新内容
        /// </summary>
        public void UpdateContent()
        {
            if (currentCorps != null)
            {
                // 更新都市数量显示
                int cityCount = 0;
                currentCorps.ForEachCity((city) => cityCount++);
                cityCountText.text = $"编制都市: {cityCount}个";
                
                // 更新军团方针显示
                policyText.text = GetPolicyText(currentCorps.appoint);
            }
            else
            {
                cityCountText.text = "编制都市: 0个";
                policyText.text = "军团方针: 无";
            }
        }
        
        /// <summary>
        /// 获取军团方针文本
        /// </summary>
        /// <param name="policyType">方针类型</param>
        /// <returns>方针文本</returns>
        private string GetPolicyText(int policyType)
        {
            switch (policyType)
            {
                case 0: return "军团方针: 攻击";
                case 1: return "军团方针: 防御";
                case 2: return "军团方针: 发展";
                case 3: return "军团方针: 平衡";
                default: return "军团方针: 无";
            }
        }
        
        /// <summary>
        /// 设置当前军团
        /// </summary>
        /// <param name="corps">军团</param>
        public void SetCurrentCorps(Corps corps)
        {
            currentCorps = corps;
            UpdateContent();
        }
        
        /// <summary>
        /// 编制都市按钮点击事件
        /// </summary>
        public void OnOrganizeCityButtonClick()
        {
            // 此处应该调用编制都市的逻辑
            Sango.Log.Info("编制都市按钮点击");
        }
        
        /// <summary>
        /// 军团方针按钮点击事件
        /// </summary>
        public void OnPolicyButtonClick()
        {
            // 此处应该调用军团方针设置的逻辑
            Sango.Log.Info("军团方针按钮点击");
        }
        
        /// <summary>
        /// 委任内容按钮点击事件
        /// </summary>
        public void OnAppointmentButtonClick()
        {
            // 此处应该调用委任内容设置的逻辑
            Sango.Log.Info("委任内容按钮点击");
        }
        
        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        public void OnCancel()
        {
            currentSystem.Exit();
        }
    }
}
