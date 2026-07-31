using Sango.UI;

namespace Sango.Core.Player
{
    [GameSystem]
    public class GameBackToMain : GameSettingMenuBase
    {
        public GameBackToMain()
        {
            customMenuName = "返回主菜单";
            customMenuOrder = 200;
        }

        public override void OnEnter()
        {
            GameDialog.Open("是否需要回到游戏主菜单", () =>
            {
                Done();
                GameDialog.Close();
                GameSystem.GetSystem<Player>().QuitToMainMenu();
            }).cancelAction = () =>
            {
                GameDialog.Close();
                Done();
            }; ;
        }

        public override void OnDestroy()
        {
            GameDialog.Close();
        }
    }
}
