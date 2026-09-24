using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 技能成功率逻辑库
    /// </summary>
    public abstract class SkillRangeFilterMethod
    {
        public SkillInstance master;
        public virtual void Init(JObject p, SkillInstance master) { this.master = master; }
        public abstract void Calculate(SkillInstance skillInstance, Troop troop, Cell where, List<Cell> spellCells);
        public virtual void Clear() { }

        public delegate SkillRangeFilterMethod SkillRangeFilterMethodCreator();

        public static Dictionary<string, SkillRangeFilterMethodCreator> CreateMap = new Dictionary<string, SkillRangeFilterMethodCreator>();
        public static void Register(string name, SkillRangeFilterMethodCreator action)
        {
            CreateMap[name] = action;
        }
        public static SkillRangeFilterMethod CraeteHandle<T>() where T : SkillRangeFilterMethod, new()
        {
            return new T();
        }
        public static SkillRangeFilterMethod Create(string name)
        {
            if(string.IsNullOrEmpty(name)) return null;

            SkillRangeFilterMethodCreator creator;
            if (CreateMap.TryGetValue(name, out creator))
                return creator();
            return null;
        }

        public static void Init()
        {
            Register("QRSLineMethod", CraeteHandle<QRSLineMethod>);

        }

        /// <summary>
        /// 需要Q,R,S,一个相等
        /// </summary>
        public class QRSLineMethod : SkillRangeFilterMethod
        {
            public override void Calculate(SkillInstance skillInstance, Troop troop, Cell where, List<Cell> spellCells)
            {
                spellCells.RemoveAll(x => !x.IsQRSLine(where));
            }
        }

    }
}
