using System;
using Newtonsoft.Json;
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
        /// 蔓延代数：由技能 / 计略直接点燃的火焰为 0，
        /// 每向外蔓延一次 +1。蔓延概率与寿命会按代数衰减，避免火势无限扩散。
        /// </summary>
        [JsonProperty]
        public int generation;

        /// <summary>
        /// 所在格子的坐标（存档形态仍是 [x, y]）
        /// </summary>
        [JsonProperty("cell")]
        [JsonConverter(typeof(CellXYConverter))]
        public CellXY cellPos = CellXY.Invalid;

        Cell mCell;
        /// <summary>
        /// 所在格子；写入 Cell 时自动同步坐标
        /// </summary>
        public Cell cell
        {
            get
            {
                if (mCell == null) mCell = cellPos.ToCell();
                return mCell;
            }
            set { mCell = value; cellPos = CellXY.From(value); }
        }

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

            // 每回合尝试向相邻格蔓延（概率取决于两格地形可燃性）
            SpreadFire(scenario);
            return true;
        }

        /// <summary>
        /// 每回合尝试向相邻格子蔓延火焰。
        ///
        /// 【概率模型】只取决于**目标格**的地形可燃性：
        ///     蔓延概率 = 目标格可燃性 / 可燃性上限 × 最大蔓延概率
        /// 可燃性取地形 <see cref="TerrainType.fireRate"/>，上限见 fireSpreadTerrainRateMax（默认 25），
        /// 最大概率见 fireSpreadMaxChance（默认 50%）。以默认值换算：
        ///     森林 / 荒地(25) → 50%
        ///     平地(20)       → 40%
        ///     湿地(5)        → 10%
        ///     水域 / 山地(0) → 0%（不会蔓延）
        ///
        /// 单团火焰每回合最多蔓延 fireSpreadMaxPerTurn 格，避免火势指数式失控。
        ///
        /// 【代数衰减】每团火焰记录自己的 <see cref="generation"/>（原始火为 0，每蔓延一次 +1）：
        ///   · generation ≥ fireSpreadMaxGeneration 时直接停止蔓延（硬上限）
        ///   · 蔓延概率按 (100 - generation × fireSpreadDecayPerGeneration)% 逐年递减
        /// 以 fireSpreadMaxGeneration=4、fireSpreadDecayPerGeneration=20 为例：
        ///     第 0 代 100% → 第 1 代 80% → 第 2 代 60% → 第 3 代 40% → 第 4 代 20% → 第 5 代停止
        /// 火势规模因此收敛，不会无限扩散。
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        public void SpreadFire(Scenario scenario)
        {
            if (cell == null || scenario == null || scenario.Variables == null)
                return;

            ScenarioVariables variables = scenario.Variables;

            // 剧本开关：关掉后火焰照常点燃 / 灼烧 / 熄灭，只是不再向相邻格蔓延
            if (!variables.fireSpreadEnabled)
                return;

            int maxChance = variables.fireSpreadMaxChance;
            int rateMax = variables.fireSpreadTerrainRateMax;
            if (maxChance <= 0 || rateMax <= 0)
                return;

            // ---------- 代数上限：达到最大代数后彻底停止蔓延 ----------
            int maxGeneration = variables.fireSpreadMaxGeneration;
            if (maxGeneration > 0 && generation >= maxGeneration)
                return;

            // ---------- 代数衰减：第 N 代只保留 (100 - N × 衰减%) 的蔓延概率 ----------
            // generation 为 0 时不衰减，保证原始火焰保持满额概率
            int decayPerGeneration = variables.fireSpreadDecayPerGeneration;
            int alivePercent = 100 - generation * decayPerGeneration;
            if (alivePercent <= 0)
                return;                             // 衰减殆尽，不再蔓延

            Cell[] neighbors = cell.Neighbors;
            if (neighbors == null)
                return;

            int maxSpread = variables.fireSpreadMaxPerTurn;
            int spreadCount = 0;

            for (int i = 0; i < neighbors.Length; i++)
            {
                if (maxSpread > 0 && spreadCount >= maxSpread)
                    break;

                Cell neighbor = neighbors[i];
                if (!CanIgnite(neighbor))
                    continue;                       // 已有火 / 水域 / 不可燃地形

                // 概率只取决于目标格的地形可燃性
                int chance = GetTerrainFireRate(neighbor) * maxChance / rateMax;
                if (chance <= 0)
                    continue;
                if (chance > maxChance)
                    chance = maxChance;             // 防止基准值配置过小导致越界

                // 【代数衰减】火势越往外的火苗越弱，越难继续扩散
                chance = chance * alivePercent / 100;
                if (chance <= 0)
                    continue;

                if (GameRandom.Chance(chance))
                {
                    Ignite(neighbor, scenario);
                    spreadCount++;
                }
            }
        }

        /// <summary>
        /// 判断目标格能否被火焰蔓延引燃。
        ///
        /// 以下情况**不可蔓延**：
        ///   · 格子为空或已有火焰（不重复点燃）
        ///   · 地形数据缺失
        ///   · 水面地形（河流 / 湖泊 / 海）
        ///   · 地形可燃性 fireRate ≤ 0（水域 / 山地 / 城池等）
        /// </summary>
        /// <param name="target">目标格</param>
        /// <returns>是否可被引燃</returns>
        static bool CanIgnite(Cell target)
        {
            if (target == null || target.fire != null)
                return false;

            TerrainType terrain = target.TerrainType;
            if (terrain == null)
                return false;

            // 水面不可燃（地形的 fireRate 通常已为 0，这里再显式兜底）
            if (terrain.isWater)
                return false;

            return terrain.fireRate > 0;
        }

        /// <summary>
        /// 取格子所在地形的可燃性（地形缺失时视为 0，即不可燃）。
        /// </summary>
        /// <param name="target">目标格</param>
        /// <returns>地形可燃性</returns>
        static int GetTerrainFireRate(Cell target)
        {
            if (target == null || target.TerrainType == null)
                return 0;
            return target.TerrainType.fireRate;
        }

        /// <summary>
        /// 在指定格子点燃一团新火焰，继承本火焰的强度。
        ///
        /// 【关键】新火焰是一个**独立存活**的 Fire 对象（IsAlive = true），
        /// 会被加入 scenario.fireSet，从而在后续每个回合的 TurnEnd 中被遍历，
        /// 也就是说**它会像父火一样继续向外蔓延**。
        ///
        /// 子火焰的代数 = 父火代数 + 1，寿命在 [1, 衰减后上限] 之间**随机**取值：
        ///   · 寿命上限同样按代数衰减，越外围的火苗存续越短
        ///   · fireSpreadMaxLifespan 为 0 时不随机，改为继承父火的剩余回合数
        /// </summary>
        /// <param name="target">目标格</param>
        /// <param name="scenario">当前剧本</param>
        void Ignite(Cell target, Scenario scenario)
        {
            if (target == null || target.fire != null || scenario == null)
                return;

            ScenarioVariables variables = scenario.Variables;
            int maxLifespan = variables != null ? variables.fireSpreadMaxLifespan : 0;
            int decayPerGeneration = variables != null ? variables.fireSpreadDecayPerGeneration : 0;

            // 子火焰代数递增
            int nextGeneration = generation + 1;

            int lifespan;
            if (maxLifespan > 0)
            {
                // 【代数衰减】寿命上限同样随代数等比缩减，越外围的火苗越短命
                int alivePercent = 100 - nextGeneration * decayPerGeneration;
                int lifespanCap = maxLifespan * alivePercent / 100;
                if (lifespanCap < 1)
                    lifespanCap = 1;                        // 至少存活 1 回合

                // GameRandom.Range 为左闭右开区间，+1 以覆盖到上限本身
                lifespan = GameRandom.Range(1, lifespanCap + 1);
            }
            else
            {
                lifespan = counter;                         // 关闭随机时沿用父火剩余回合
            }

            Fire spread = new Fire()
            {
                damage = damage,
                intelligence = intelligence,
                cell = target,
                generation = nextGeneration,                // 记录蔓延代数
                counter = lifespan,                         // 独立随机寿命，不影响父火
            };

            scenario.Add(spread);
            spread.Init(scenario);

            Sango.Log.Info($"火焰蔓延至 ({target.x},{target.y}) 地形可燃性:{GetTerrainFireRate(target)}");
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
