using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    public class BuffEffect
    {
        public BuffInstance master;
        public virtual void Init(JObject p, BuffInstance master) { this.master = master; }
        public virtual void Action(BuffInstance BuffInstance, Troop troop, Cell spellCell, List<Cell> atkCellList) {; }
        public virtual void Clear() {; }

        public delegate BuffEffect BuffEffectCreator();

        public static Dictionary<string, BuffEffectCreator> CreateMap = new Dictionary<string, BuffEffectCreator>();
        public static void Register(string name, BuffEffectCreator action)
        {
            CreateMap[name] = action;
        }
        public static BuffEffect CraeteHandle<T>() where T : BuffEffect, new()
        {
            return new T();
        }
        public static BuffEffect Create(string name)
        {
            BuffEffectCreator creator;
            if (CreateMap.TryGetValue(name, out creator))
                return creator();
            return null;
        }

        public static void Init()
        {
            Register("Stun", CraeteHandle<Stun>);
            Register("Escape", CraeteHandle<Escape>);
            Register("Poison", CraeteHandle<Poison>);
            Register("Burn", CraeteHandle<Burn>);
            Register("Freeze", CraeteHandle<Freeze>);
            Register("Silence", CraeteHandle<Silence>);
            Register("Invincible", CraeteHandle<Invincible>);
            Register("Shield", CraeteHandle<Shield>);
            Register("AddAttack", CraeteHandle<AddAttack>);
            Register("AddDefence", CraeteHandle<AddDefence>);
            //Register("AddBuff", CraeteHandle<AddBuff>);

        }





    }
}
