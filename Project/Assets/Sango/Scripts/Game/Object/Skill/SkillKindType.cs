using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Sango.Core
{
    public enum SkillKindType : int
    {
        None = 0,

        /// <summary>
        /// 普攻
        /// </summary>
        Normal = 1,

        /// <summary>
        /// 战法
        /// </summary>
        Tactics = 2,

        /// <summary>
        /// 计策
        /// </summary>
        Stratagem = 3,

        /// <summary>
        /// 战意
        /// </summary>
        Ultimate = 4
    }
}
