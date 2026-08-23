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
            GameDialog.Instance.Open(GameDialog.DialogStyle.Normal, "是否需要回到游戏主菜单", () =>
           {
               Done();
               GameSystem.GetSystem<Player>().QuitToMainMenu();
           }
            , () =>
             {
                 Done();
             });
        }

        public override void OnDestroy()
        {

        }
    }
}
