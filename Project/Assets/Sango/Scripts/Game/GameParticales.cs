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
        /// <summary>
        /// 特效播放总开关。顶视图（全局俯瞰）期间由 MapTopView 置 false：
        /// 那一帧画面只保留地形，新冒出来的特效会直接破坏观感。
        /// 关闭时 <see cref="PlayEfect(string, Vector3, float)"/> 返回 null，调用方都已做空判断。
        /// </summary>
        public static bool EffectEnabled = true;

        public GameObject PlayEfect(string assets, Vector3 where, float life)
        {
            if (!EffectEnabled) return null;
            return PlayEfect(assets, where, Vector3.one, Quaternion.identity, life);
        }

        public GameObject PlayEfect(string assets, Vector3 where, Vector3 scale, Quaternion rot, float life)
        {
            if (!EffectEnabled) return null;

            GameObject @object = PoolManager.Create(assets);
            if (@object != null)
            {
                @object.transform.SetParent(null, false);
                @object.transform.position = where;
                @object.transform.localScale = scale;
                @object.transform.localRotation = rot;

                PoolLife.AutoRelease(@object, life);
            }
            return @object;
        }
    }

}
