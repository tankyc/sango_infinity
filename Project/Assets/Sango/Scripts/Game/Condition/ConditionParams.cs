using System;

namespace Sango.Game.Condition
{
    public class ConditionParams
    {
        public virtual City City { get; set; }
        public virtual Building Building { get; set; }
        public virtual Person Person { get; set; }
        public virtual Scenario Scenario { get; set; }
        public virtual Force Force { get; set; }
        public virtual Corps Corps { get; set; }
        public virtual Skill Skill { get; set; }
        public virtual Troop Troop { get; set; }
    }
}