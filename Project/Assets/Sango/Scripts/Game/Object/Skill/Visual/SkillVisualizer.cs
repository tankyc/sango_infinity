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
            Register("CyclonSkill", CreateHandle<CyclonSkillVisualizer>);
            Register("ThunderboltSkill", CreateHandle<ThunderboltSkillVisualizer>);
            Register("DemonSkill", CreateHandle<DemonSkillVisualizer>);
        }
    }

    public class DefaultSkillVisualizer : SkillVisualizer
    {
        public override void PlaySkillVisual(Troop troop, Cell spellCell, List<Cell> atkCellList)
        {
            // 默认技能视觉效果
            troop.Render.SetAniShow(1);
            troop.Render.FaceTo(spellCell.Position);
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
            troop.Render.SetAniShow(1);
            troop.Render.FaceTo(spellCell.Position);
        }
    }

    public class StrategySkillVisualizer : SkillVisualizer
    {
        const string StrategyEffectAsset = "Assets/Effect/Prefab/ef_thunder_02.prefab";

        public override void PlaySkillVisual(Troop troop, Cell spellCell, List<Cell> atkCellList)
        {
            troop.Render.SetAniShow(3);
            troop.Render.FaceTo(spellCell.Position);
            foreach (var cell in atkCellList)
            {
                troop.Render.PlayEffect(StrategyEffectAsset);
            }
        }
    }


    public class CyclonSkillVisualizer : MeleeSkillVisualizer
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
                }
                else
                    Sango.Log.Error($"旋风特效播放失败: sheet={CycloneSheetPath}", Sango.Log.LogType.World);
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

    public class ThunderboltSkillVisualizer : StrategySkillVisualizer
    {
        // 计策特效预制体资源路径(可替换为其他特效)
        const string StrategyEffectAsset = "Assets/Effect/Prefab/ef_thunder_02.prefab";

        // 落雷第一段(全屏):挂 UIFullScreenFx 组件,16个图层在 Inspector 可视编辑
        const string FullScreenFxAsset = "Assets/Effect/Prefab/ef_fx_thunder_full.prefab";

        // 落雷第二段(普通特效,非全屏):519/521/522 预制体(经 GameParticales 池化播放,挂 SpriteSequenceEffect)
        // 命名与素材编号一一对应:ef_thunder_20=519, ef_thunder_21=521, ef_thunder_22=522
        const string Bolt519EffectAsset = "Assets/Effect/Prefab/ef_thunder_20.prefab";
        const string Bolt521EffectAsset = "Assets/Effect/Prefab/ef_thunder_21.prefab";
        const string Bolt522EffectAsset = "Assets/Effect/Prefab/ef_thunder_22.prefab";
        // 落雷高度(屏幕空间百分比:0=底,1=顶) —— 沿 cam.up 方向偏移,任何视角都正确
        //   522 贴地 → 521 光球(地格+Y偏移:0.50/1.0/2.0×grid) → 519 黑云(最高:8.0×grid)
        static readonly float[] Bolt521HeightRates = { 0.5f, 2.0f, 3.0f };
        const float Bolt519HeightRate = 5.0f;

        public override void PlaySkillVisual(Troop troop, Cell spellCell, List<Cell> atkCellList)
        {
            troop.Render.SetAniShow(3);
            troop.Render.FaceTo(spellCell.Position);
            UIFullScreenFx.PlayFx(FullScreenFxAsset);

            float grid = Scenario.Cur != null && Scenario.Cur.Map != null && Scenario.Cur.Map.GridSize > 0f
                ? Scenario.Cur.Map.GridSize : 20f;
            List<Cell> targetCells = new List<Cell>();
            for (int i = 0; i < atkCellList.Count; i++)
            {
                Cell c = atkCellList[i];
                if (c != null && (c.troop != null || c.building != null))
                    targetCells.Add(c);
            }
            if (targetCells.Count == 0)
                targetCells.Add(spellCell);
            // === 522 === 闪电光柱:底部贴地格,直接用地格世界坐标
            foreach (Cell atkCell in targetCells)
            {
                GameParticales.Instance.PlayEfect(Bolt522EffectAsset, atkCell.Position, 15f);
            }
            // === 519 === 黑云:沿相机up方向偏移,任何视角都保持在落雷正上方(只主目标)
            Camera cam = Camera.main;
            Vector3 camUp = cam != null ? cam.transform.up : Vector3.up;
            Vector3 bolt19Pos = spellCell.Position + camUp * (grid * Bolt519HeightRate);
            GameParticales.Instance.PlayEfect(Bolt519EffectAsset, bolt19Pos, 15f);
            // === 521 === 三个光球:对每个目标格都播放
            float[] bolt23Delays = { 2.405f, 2.305f, 2.205f };
            foreach (Cell atkCell in targetCells)
            {
                Vector3 basePos = atkCell.Position;
                // 下球:Y=0.5*grid; 中球:Y=2.0*grid; 上球:Y=3.0*grid; X左右散开; Z错开防重叠
                Vector3[] bolt23Pos =
                {
                    basePos + new Vector3( grid * 0.0f, grid * Bolt521HeightRates[0], -grid * 0.3f),
                    basePos + new Vector3(-grid * 0.5f, grid * Bolt521HeightRates[1],  grid * 0.0f),
                    basePos + new Vector3( grid * 0.5f, grid * Bolt521HeightRates[2],  grid * 0.3f)
                };
                Vector3[] bolt23Scales = { Vector3.one, Vector3.one * 0.8f, Vector3.one * 0.8f };
                for (int i = 0; i < bolt23Pos.Length; i++)
                {
                    GameObject g = GameParticales.Instance.PlayEfect(Bolt521EffectAsset, bolt23Pos[i], 15f);
                    if (g != null)
                    {
                        g.transform.localScale = bolt23Scales[i];
                        var seq = g.GetComponent<SpriteSequenceEffect>();
                        if (seq != null)
                            seq.SetStartDelay(bolt23Delays[i]);
                    }
                }
            }
        }
    }

    public class DemonSkillVisualizer : StrategySkillVisualizer
    {
        // 妖术第一段(全屏): 复用落雷全屏时序,496-1替换497-1,523替换125-3
        const string FullScreenFxAsset = "Assets/Effect/Prefab/ef_fx_demon_full.prefab";

        // 妖术第二段(普通特效,非全屏):只播 524
        const string Bolt524EffectAsset = "Assets/Effect/Prefab/ef_demon_524.prefab";

        public override void PlaySkillVisual(Troop troop, Cell spellCell, List<Cell> atkCellList)
        {
            troop.Render.SetAniShow(3);
            troop.Render.FaceTo(spellCell.Position);
            UIFullScreenFx.PlayFx(FullScreenFxAsset);

            float grid = Scenario.Cur != null && Scenario.Cur.Map != null && Scenario.Cur.Map.GridSize > 0f
                ? Scenario.Cur.Map.GridSize : 20f;

            // 524:主目标只播 1 次
            GameParticales.Instance.PlayEfect(Bolt524EffectAsset, spellCell.Position, 5f);
        }
    }


}