using ContextMenu = Sango.UI.ContextMenu;

namespace Sango.Core.Player
{
    /// <summary>
    /// 建筑拆除
    /// </summary>
    [GameSystem]
    public class BuildingActionDestroy : BuildingActionBase
    {
        public BuildingActionDestroy()
        {
            customMenuName = "拆除";
            customMenuOrder = 10000;
        }
        public override bool IsValid
        {
            get
            {
                return true;
            }
        }

        public override void OnEnter()
        {
            ContextMenu.CloseAll();
            base.OnEnter();

            GameDialog.IDialog dialog = GameDialog.Open(GameDialog.DialogStyle.ChoosePersonSay,
                $"拆除{TargetBuilding.ColorName}后,将无法恢复,确定吗?",
                   () =>
                   {
                       GameDialog.Close();
                       TargetBuilding.OnFall(null);
                       Done();
                   });
            dialog.cancelAction = () =>
            {
                GameDialog.Close();
                Done();
            };

            Force force = TargetBuilding.mForce;
            //GameDialog.IDialog dialog = GameDialog.Open(GameDialog.DialogStyle.ClickPersonSay, $"{force.ColorName}大人，\n终于轮到我们了啊。", null);
            Person person = force.mCounsellor;
            if (person == null || person.mForce != force)
            {
                int max = TargetBuilding.mCity.allPersons.Count;
                if (max > 0)
                {
                    person = TargetBuilding.mCity.allPersons.Get(GameRandom.Range(0, max));
                }
                else
                {
                    person = force.mGovernor;
                }
            }
            dialog.SetPerson(person);
        }
    }
}
