using Sango.Render;
using System.Collections.Generic;
using UnityEngine;

namespace Sango.Core
{
    public abstract class SkillVisualizer
    {
        public SkillInstance skillInstance;
        public virtual void Init(SkillInstance skillInstance) { this.skillInstance = skillInstance; }
        public abstract void PlaySkillVisual(Troop troop, Cell spellCell, List<Cell> atkCellList);
        public virtual void StopSkillVisual() { }

        public delegate SkillVisualizer SkillVisualizerCreator();

        public static Dictionary<string, SkillVisualizerCreator> CreateMap = new Dictionary<string, SkillVisualizerCreator>();
        public static void Register(string name, SkillVisualizerCreator creator)
        {
            CreateMap[name] = creator;
        }
        public static SkillVisualizer CreateHandle<T>() where T : SkillVisualizer, new()
        {
            return new T();
        }
        public static SkillVisualizer Create(string name)
        {
            SkillVisualizerCreator creator;
            if (CreateMap.TryGetValue(name, out creator))
                return creator();
            return null;
        }

        public static void Init()
        {
            Register("Default", CreateHandle<DefaultSkillVisualizer>);
            Register("Range", CreateHandle<RangeSkillVisualizer>);
            Register("Melee", CreateHandle<MeleeSkillVisualizer>);
            Register("Strategy", CreateHandle<StrategySkillVisualizer>);
        }
    }

    public class DefaultSkillVisualizer : SkillVisualizer
    {
        // 近战命中特效预制体(普通攻击/近战战法通用)
        const string MeleeHitEffectAsset = "Assets/Effect/Prefab/ef_titans_attack_1.prefab";

        // 旋风战法(技能Id=8)特效：1张精灵图集(7列x4行=28帧)逐帧播放
        const int CycloneSkillId = 8;
        const string CycloneSheetPath = "Assets/Effect/Sprite/Cyclone/cyclone_sheet.png";
        const int CycloneSheetCols = 7;
        const int CycloneSheetRows = 4;
        const int CycloneFrameCount = 28;
        const float CycloneFps = 20f;
        const float CycloneWorldSize = 150f;

        public override void PlaySkillVisual(Troop troop, Cell spellCell, List<Cell> atkCellList)
        {
            // 默认技能视觉效果
            troop.Render.SetAniShow(1);
            troop.Render.FaceTo(spellCell.Position);

            if (skillInstance != null && skillInstance.Id == CycloneSkillId)
            {
                // 旋风战法：在【施法者部队所在格子】只放 1 个大旋风（不再遍历受击目标）
                Cell casterCell = troop.cell ?? spellCell;
                Vector3 pos = casterCell.Position + Vector3.up * 3f;
                var eff = SpriteSequenceEffect.Play(CycloneSheetPath, CycloneSheetCols, CycloneSheetRows,
                                                     CycloneFrameCount, pos, fps: CycloneFps, worldSize: CycloneWorldSize);
                if (eff != null)
                {
                    eff.autoDestroy = true;
                    Debug.Log($"旋风特效已播放 @施法者格子 caster={casterCell.Position} worldSize={CycloneWorldSize}");
                }
                else
                    Debug.LogError($"旋风特效播放失败: sheet={CycloneSheetPath}");
                return;
            }

            // 在受击目标格子播放近战命中特效
            foreach (var cell in atkCellList)
            {
                PlayMeleeHitEffect(troop, cell);
            }
        }

        // 在目标格子播放近战命中特效
        void PlayMeleeHitEffect(Troop troop, Cell cell)
        {
            GameObject effect = troop.Render.PlayEffect(MeleeHitEffectAsset);
            if (effect != null)
            {
                // 挂载到部队节点后定位到目标格子世界坐标
                effect.transform.position = cell.Position;
            }
        }
    }

    public class RangeSkillVisualizer : SkillVisualizer
    {
        public override void PlaySkillVisual(Troop troop, Cell spellCell, List<Cell> atkCellList)
        {
            // 远程技能视觉效果
            troop.Render.SetAniShow(1, true);
            troop.Render.FaceTo(spellCell.Position);
            // 发射箭头特效
            troop.Render.CastArrow(spellCell.Position);
        }
    }

    public class MeleeSkillVisualizer : SkillVisualizer
    {
        public override void PlaySkillVisual(Troop troop, Cell spellCell, List<Cell> atkCellList)
        {
            // 近战技能视觉效果
            troop.Render.SetAniShow(2);
            troop.Render.FaceTo(spellCell.Position);
        }
    }

    public class StrategySkillVisualizer : SkillVisualizer
    {
        // 计策特效预制体资源路径(可替换为其他特效)
        const string StrategyEffectAsset = "Assets/Effect/Prefab/ef_thunder_02.prefab";

        public override void PlaySkillVisual(Troop troop, Cell spellCell, List<Cell> atkCellList)
        {
            // 计策技能视觉效果
            troop.Render.SetAniShow(3);
            troop.Render.FaceTo(spellCell.Position);
            // 在每个目标格子上显示计策特效
            foreach (var cell in atkCellList)
            {
                troop.Render.PlayEffect(StrategyEffectAsset);
            }
        }
    }
}
