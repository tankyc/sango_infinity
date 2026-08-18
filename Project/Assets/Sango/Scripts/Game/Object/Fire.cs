using TKNewtonsoft.Json;
using Sango.Render;

namespace Sango.Core
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Fire : SangoObject
    {
        /// <summary>
        /// 获取对象类型
        /// </summary>
        public override SangoObjectType ObjectType => SangoObjectType.Fire;

        [JsonProperty]
        public int damage;

        [JsonProperty]
        public int intelligence;

        [JsonProperty]
        public int counter;

        /// <summary>
        /// 所在格子
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(XY2CellConverter))]
        public Cell cell;

        public FireRender Render { get; private set; }

        public override void Init(Scenario scenario)
        {
            cell.fire = this;
            Render = new FireRender(this);
        }

        public override void OnScenarioPrepare(Scenario scenario)
        {

        }

        public override bool OnTurnStart(Scenario scenario)
        {
            ActionOver = false;
            counter--;
            if (counter <= 0)
            {
                Clear();
            }
            return true;
        }

        public override bool OnTurnEnd(Scenario scenario)
        {
            if (!ActionOver)
            {
                Action();
            }
            return true;
        }

        public override void Clear()
        {
            base.Clear();

            if(cell != null)
                cell.fire = null;

            if (Render != null)
            {
                Render.Clear();
                Render = null;
            }

            Scenario.Cur.Remove(this);
            ActionOver = true;
            IsAlive = false;
        }

        public void Action()
        {
            if (cell.troop != null)
            {
                BurnTroop(cell.troop);
            }
            else if (cell.building != null)
            {
                BurnBuildiong(cell.building);
            }
            ActionOver = true;
        }
        public void BurnTroop(Troop troop)
        {
            if (troop == null) return;
            if (troop.ignoreFire) return;

            int dmg = damage + intelligence * 2 - troop.Intelligence - troop.Defence;
           // if (troop.ChangeTroops(-dmg, this, false))
            {
                FireDamageEvent @event = RenderEvent.Instance.Create<FireDamageEvent>();
                @event.Init(this, dmg, troop, null);
                RenderEvent.Instance.Add(@event);
            }
        }

        public void BurnBuildiong(BuildingBase building)
        {
            if (building == null) return;

            // 火焰不能决定城池归属
            int dmg = damage / 3 + intelligence / 2;
            if (building.durability < dmg)
                dmg = building.durability - 1;
            if (dmg > 0)
            {
                //building.ChangeDurability(-dmg, this, false);
                FireDamageEvent @event = RenderEvent.Instance.Create<FireDamageEvent>();
                @event.Init(this, dmg, null, building);
                RenderEvent.Instance.Add(@event);
            }
        }

    }
}
