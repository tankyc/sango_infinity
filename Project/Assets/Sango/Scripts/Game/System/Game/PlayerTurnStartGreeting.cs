using Sango.Render;
using Sango.UI;

namespace Sango.Core.Player
{
    [GameSystem]
    public class PlayerTurnStartGreeting : GameSystem
    {
        public PlayerTurnStartGreeting()
        {

        }

        public override void OnEnter()
        {
            OnBack(null);
        }

        public override void OnBack(ICommandEvent whoGone)
        {
            Force force = Scenario.Cur?.CurRunForce;
            if (force == null)
            {
                // 回合开始时机不对 / 势力已被移除：问候是纯表现层，不能因此卡住整个回合流程
                Sango.Log.Error("*[回合开始] 玩家回合开场问候跳过：当前运行势力为空");
                Done();
                return;
            }

            //GameDialog.IDialog dialog = GameDialog.Open(GameDialog.DialogStyle.ClickPersonSay, $"{force.ColorName}大人，\n终于轮到我们了啊。", null);
            Person person = force.mCounsellor;
            if (person == null || person.BelongForce != force)
            {
                // 【兜底】原写法直接取 force.CapitalCity.allPersons，
                // 而 CapitalCity 是派生值（Force.cs:174 `mGovernor?.BelongCity`）：
                // 君主缺失、君主尚未配城（剧本编辑器新建势力 / 君主被俘后尚未指派）时它为 null，
                // 这里会抛 NullReferenceException，把整个回合推进卡死在这一步。
                person = PickGreetingPerson(force);
            }

            if (person == null)
            {
                // 连一个可用武将都没有（势力空壳）：跳过问候，别用 null 去播放语音 / 弹对话
                Sango.Log.Warning($"[回合开始]{force.Name} 没有可用于开场问候的武将，已跳过开场问候");
                Done();
                return;
            }

            GameMedia.Instance.PlaySfx(32);
            PlayerMessage.AddPersonMessage($"下一步，我们该干些什么呢？。", person);
            DialogEvent te = RenderEvent.Instance.Create<DialogEvent>();
            te.Init(GameDialog.DialogStyle.ClickPersonSay, $"{force.ColorName}大人，\n终于轮到我们了啊。", person, null, () =>
            {
                Done();
            });
            RenderEvent.Instance.Add(te);

        }

        /// <summary>
        /// 挑一个用于开场问候的武将。
        ///
        /// 取值顺序（逐级兜底，任何一级为空都继续往下，不抛异常）：
        /// <list type="number">
        /// <item>都城内在城武将（随机一人，保持原有观感）；</item>
        /// <item>势力君主（都城派生链断了但君主还在时）；</item>
        /// <item>势力内任意存活的在籍武将（空壳势力兜底）。</item>
        /// </list>
        /// </summary>
        /// <param name="force">当前运行的玩家势力</param>
        /// <returns>可用于问候的武将；全都找不到时返回 null</returns>
        static Person PickGreetingPerson(Force force)
        {
            if (force == null)
                return null;

            City capital = force.CapitalCity;
            if (capital != null && capital.allPersons != null && capital.allPersons.Count > 0)
                return capital.allPersons.Get(GameRandom.Range(0, capital.allPersons.Count));

            if (force.mGovernor != null)
                return force.mGovernor;

            Scenario scenario = Scenario.Cur;
            if (scenario != null && scenario.personSet != null)
            {
                Person found = null;
                scenario.personSet.ForEach(p =>
                {
                    if (found != null || p == null || !p.IsAlive)
                        return;
                    if (p.BelongForce == force)
                        found = p;
                });
                return found;
            }

            return null;
        }

        public override void OnDestroy()
        {
            
        }

        public override void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
        {
            switch (eventType)
            {
                case CommandEventType.Cancel:
                case CommandEventType.ClickDown:
                case CommandEventType.RClickDown:
                    Back(); break;
            }

        }
    }
}
