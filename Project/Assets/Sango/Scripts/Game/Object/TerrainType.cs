using Newtonsoft.Json;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Sango.Core
{
    [JsonObject(MemberSerialization.OptOut)]
    public class TerrainType : SangoObjectExtensionData
    {
        public int foodDeposit;
        public float[] fertility = new float[2];
        public int goldDeposit;
        public float[] prosperity = new float[2];

        public float[] foodRate = new float[4];
        public ushort foodRegainDays;
        public float fireDamageRate;
        public bool viewThrough;
        public bool canBuild;
        public bool moveable;
        public bool canAmbush;
        public int baseCost;
        public bool isWater;
        public int fireRate;

        /// <summary>
        /// AI 位置评分档（对应原版 get_pos_terrain_score 的地形分支，s11_sys_unit_action.cpp:1567-1588）。
        ///
        ///   0 = 普通（不加分）
        ///   1 = 砂地      → +40
        ///   2 = 湿地      → +30
        ///   3 = 毒泉      → -10000（部队有「解毒」特技时 +150）
        ///   4 = 森林      → +80
        ///   5 = 栈道      → -250（有「难所行军」科技或「藤甲」特技时 +70）
        ///
        /// 【为什么用档位而不是地形 Id】地图编辑器可以改地形 Id 顺序，
        /// AI 只应该认"这块地是什么性质"，与具体 Id 无关。
        /// 具体分值见 AIConfig.o311PosTerrain*。
        /// </summary>
        public int posKind;

        /// <summary>
        /// 火计火势系数（对应原版 FireTerrainCoef，s11_sys_anim_unit_strategy.cpp:8-10）。
        ///
        /// 原版表（TerrainId 0~19）：
        ///   草原/土/砂 40、湿地/毒泉 10、森林 **50**、小河/河/海 0、
        ///   荒地 35、大路 30、栈道 15、桥 20、渡口 10、水边/崖/城/港/关 0、小径 10
        ///
        /// 100 = 该格已着火（原版 `if (a == 100) return {a, false}` 直接必中），
        /// 本项目由 `cell.fire != null` 判断，不需要在此配置。
        /// </summary>
        public int fireCoef;

        // ==================== 移动伤害（原版 get_movement_damage，s11_sys_damage.cpp:5-44） ====================
        //
        // 原版：部队每进入一个格子，按地形结算一次兵力损失。
        //   毒泉  200 + rand(200)，有「解毒」特技免疫
        //   栈道  100 + rand(200)，有「踏破」特技 / 「南蛮」科技免疫
        //   着火   50 + rand(50)， 有「藤甲」特技则**翻倍**
        //
        // 本项目的着火伤害由 Cell.fire.BurnTroop 单独处理（已有），
        // 这里只配置"地形本身的伤害"，参数全部来自数据，代码不认地形 Id。

        /// <summary>进入本格时的基础兵力伤害（0 = 无伤害）</summary>
        public int moveDamageBase;

        /// <summary>进入本格时的随机伤害上限：实际伤害 = 基础 + rand(0, 本值)</summary>
        public int moveDamageRandom;

        /// <summary>
        /// 免疫该移动伤害的**部队标记位**（<see cref="PathFlag"/> 的位掩码，0 = 无免疫）。
        ///
        /// 【为什么用 bit 位而不是特技 Id】免疫来源有很多：特技（解毒/踏破/藤甲）、
        /// 科技（难所行军）、事件、装备…… 它们最终都只是给部队打一个"能无视毒泉"的标记。
        /// 数据只需要声明"本格需要哪个标记才免疫"，不必关心标记是谁给的。
        ///
        ///   毒泉 → <c>PathFlag.IgnorePoison</c> = 1
        ///   栈道 → <c>PathFlag.IgnoreCliffroad</c> = 2
        ///
        /// 与 <see cref="PathAlignment"/> 的寻路豁免共用同一套位，寻路免代价与免伤害天然一致。
        /// </summary>
        public int moveImmunityFlag;

        [JsonConverter(typeof(Color32Converter))]
        public Color32 color;


        public float Fertility
        {
            get
            {

                return GameRandom.Range(fertility[0], fertility[1]);
            }
        }

        public float Prosperity
        {
            get
            {
                return GameRandom.Range(prosperity[0], prosperity[1]);
            }
        }

        public int GetFood(int season, bool isRandom)
        {
            if (isRandom)
                return GetRandomFood(season);
            else
                return (int)(foodDeposit * foodRate[season]);
        }

        public int GetRandomFood(int season)
        {
            int num = GameUtility.Random(foodDeposit / 2) + ((foodDeposit * 3) / 4);
            return (int)(num * foodRate[season]);
        }
        public bool CanMoveBy(Troop troops)
        {
            return moveable;
        }

        //public override void Load(BinaryReader reader)
        //{
        //    base.Load(reader);
        //    Name = reader.ReadString();
        //    foodDeposit = reader.ReadInt32();
        //    for (int i = 0; i < 4; i++)
        //    {
        //        foodRate[i] = reader.ReadSingle();
        //    }
        //    foodRegainDays = reader.ReadUInt16();
        //    fireDamageRate = reader.ReadSingle();
        //    canBuild = reader.ReadBoolean();
        //    viewThrough = reader.ReadBoolean();
        //    moveable = reader.ReadBoolean();
        //}

        //public override void Save(BinaryWriter writer)
        //{
        //    base.Save(writer);
        //    writer.Write(Name);
        //    writer.Write(foodDeposit);
        //    for (int i = 0; i < 4; i++)
        //    {
        //        writer.Write(foodRate[i]);
        //    }
        //    writer.Write(foodRegainDays);
        //    writer.Write(fireDamageRate);
        //    writer.Write(canBuild);
        //    writer.Write(viewThrough);
        //    writer.Write(moveable);
        //}
    }
}
