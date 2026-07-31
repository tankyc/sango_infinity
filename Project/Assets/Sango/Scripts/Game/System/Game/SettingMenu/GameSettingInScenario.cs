using Sango.UI;

namespace Sango.Core.Player
{
    /// <summary>
    /// 游戏中设置按钮 - 在游戏场景中打开游戏设置界面
    /// 继承自GameSettingMenuBase，通过设置菜单打开window_game_setting界面
    /// </summary>
    [GameSystem]
    public class GameSettingInScenario : GameSettingMenuBase
    {
        /// <summary>
        /// 构造函数 - 注册菜单项名称和排序
        /// </summary>
        public GameSettingInScenario()
        {
            customMenuName = "设置";
            customMenuOrder = 100;
        }

        /// <summary>
        /// 进入设置命令 - 打开游戏设置窗口
        /// </summary>
        public override void OnEnter()
        {
            Window.Instance.Open("window_game_setting", (System.Action)OnSure, (System.Action)OnCancel);
        }

        void OnSure()
        {
            Done();
        }

        void OnCancel()
        {
            Done();
        }

        /// <summary>
        /// 离开设置命令时关闭游戏设置窗口
        /// </summary>
        public override void OnDestroy()
        {
            Window.Instance.Close("window_game_setting");
        }
    }
}
