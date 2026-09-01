/*
 * 文件名：GameFormula.cs
 * 描述：游戏公式类，包含游戏中使用的各种计算公式
 * 创建日期：2026-03-27
 * 最后修改：2026-03-27
 */

using System;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 游戏公式类，包含游戏中使用的各种计算公式
    /// 如招募概率、伤害计算、成功率计算等
    /// </summary>
    public class GameParticales : Singleton<GameParticales>
    {
        public void PlayEfect(string assets, Vector3 where, float life)
        {
            PlayEfect(assets, where, Vector3.one, Quaternion.identity, life);
        }

        public void PlayEfect(string assets, Vector3 where, Vector3 scale, Quaternion rot, float life)
        {
            GameObject @object = PoolManager.Create(assets);
            if (@object != null)
            {
                @object.transform.SetParent(null, false);
                @object.transform.position = where;
                @object.transform.localScale = scale;
                @object.transform.localRotation = rot;

                PoolLife poolLife = @object.GetComponent<PoolLife>();
                if (poolLife == null)
                    poolLife = @object.AddComponent<PoolLife>();

                poolLife.life = life;
            }
        }
    }

}
