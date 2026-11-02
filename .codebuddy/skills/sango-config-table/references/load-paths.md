# 配置表加载链路清单

改任何 `Data/**/*.json` 之前先在本表定位它属于哪条链路。三条链路对未知字段 / 缺失字段的处置完全不同。

---

## 链路 A：`Data/Common` 全目录 `PopulateObject`

### 入口

```68:91:Project/Assets/Sango/Scripts/Game/GameData.cs
                Sango.Directory.EnumFiles(Path.ContentRootPath + "/Data/Common", file =>
                {
                    ScenarioCommonData.Load(file);
                });
```

`ScenarioCommonData.Load` 的实现只有两行：

```236:242:Project/Assets/Sango/Scripts/Game/Scenario/ScenarioCommonData.cs
        public void Load(string file)
        {
            if (File.Exists(file))
            {
                Newtonsoft.Json.JsonConvert.PopulateObject(File.ReadAllText(file), this);
            }
        }
```

扫描范围三处：
1. `<ContentRoot>/Data/Common`（即 `Build/Content/Data/Common`，递归）
2. `<CustomEditRoot>/Data/Common`（`LoadNewCommonData` 才有）
3. 各启用 Mod 的 `Data/Common`（`EnumFiles`，`AllDirectories`）

### 行为

- `ScenarioCommonData` 标了 `[JsonObject(MemberSerialization.OptIn)]`，只有带 `[JsonProperty]` 的成员参与填充。
- 未知顶层键 **静默忽略**，无日志、无异常。
- `PopulateObject` 是**合并**语义：对已存在的集合属性做填充，不是整体替换。

### 保留顶层键：什么时候正常、什么时候是坑

`ScenarioCommonData` 下面这 25 个成员带 `[JsonProperty]`：

```
TerrainTypes        BuildingTypes       Features            Abilities
TroopTypes          ItemTypes           TroopAnimations     AttributeChangeTypes
PersonAttributeTypes CityLevelTypes     Flags               Provinces
Regions             Titles              Officials           Nations
Skills              PersonLevels        JobTypes            Buffs
Techniques          Personalities       Argumentations      AbilityLevelTypes
PersonLibrary
```

**正常情况**：`Data/Common/` 里的 `Features.json` 顶层键就叫 `Features`，`Skills.json` 顶层就叫 `Skills`……这些表的设计意图就是「让 `PopulateObject` 把内容填进对应集合」。顶层**只有**一个保留名（+ `_comment`）时，属于链路 A 的正常表，不要去改名。

**坑**：新建一张有自己专属加载器（链路 B/C）的表，顶层却混进了一个保留名，例如

```json
{ "_comment": "...", "Skills": [...], "entries": [...] }
```

此时 `Skills` 会被 `PopulateObject` 合并进全局技能表——数据凭空多出来、内容格式不对还可能直接污染运行时对象，而**没有任何日志**。顶层出现「保留名 + 其它业务键」的组合，一律视为误撞，必须改名。

安全做法：自建表的顶层键用 `entries` / 业务自定名 / `_comment`，不碰上面 25 个词。

### 受影响的表

`Build/Content/Data/Common/` 下**全部** json（`Abilities.json`、`Features.json`、`JobTypes.json`、`Skills.json`、`Titles.json` 等 31 个）。它们在链路 A 里被扫一遍，若同时还有专属加载器（链路 B/C），则会再被读一遍——两遍互不干扰，但**改字段时要按更严格的那条链路判断**。

---

## 链路 B：整文件覆盖 + 稀疏回填（最危险）

### 特征

`ModManager.Instance.LoadFile(path, callback)` 取到文件后，用 `JObject` 手工取值或用 `ToObject<T>()` 逐行回填。**未写的字段取类型默认值（int = 0），不会沿用 C# 内置值。**

### 清单

| 文件 | 加载代码 | 回填方式 |
|---|---|---|
| `Data/Common/CombatExpTable.json` | `CombatExpTable.cs:324` | `item.ToObject<CombatExpEntry>()`，按 `event` 索引覆盖内置表 |
| `Data/Common/GainValueConfig.json` | `GainValueConfig.cs:600` | 手工 `JObject` 遍历，键名 = 枚举名，`_` 开头跳过 |

这两张表都是「C# 内置默认值 + JSON 覆盖层」结构，改数值必须两处同步，详见 SKILL.md 的「双落点陷阱」。

`GainValueConfig` 支持三种值形态：整数、`{ "min": x, "max": y }`（区间）、以及以 `_` 开头的注释键（`GainValueConfig.cs:636` 显式跳过）。

---

## 链路 C：强类型整表反序列化

`ModManager.Instance.LoadFile` + `JsonConvert.DeserializeObject<T>(json)`。未知键静默忽略（`MissingMemberHandling.Ignore` 为默认值），缺失字段取字段初始值。

| 文件 | 加载代码 |
|---|---|
| `Data/Common/AIConfig.json` | `AIConfig.cs:935` |
| `Data/Common/RecommendedTroopTeams.json` | `TroopTeamLibrary.cs:307` |
| `Data/Common/duelPersonBehaviour.json` | `DuelPersonBehaviours.cs:295` 的 `ConfigPath`，加载在 `:330` |
| `Data/Common/debatePersonBehaviour.json` | `DebatePersonBehaviours.cs:176` 的 `ConfigPath`，加载在 `:241` |
| `Data/Model/ModelConfig.json` | `GameData.cs:100` |

`SkillConfigManager` 走同一形态，但路径写的是 `Data/Skill/Skills.json` / `Data/Skill/Buffs.json`（`SkillConfigManager.cs:43`、`:71`）。内容根目录下**没有** `Data/Skill/` 目录，所以内置数据实际由链路 A 从 `Data/Common/Skills.json` 加载；`Data/Skill/` 这条只对提供了该目录的 Mod 生效。改技能表时以 `Data/Common/Skills.json` 为准。

---

## `ModManager.LoadFile` 的查找顺序

```691:715:Project/Assets/Sango/Scripts/Game/Mod/ModManager.cs
        public void LoadFile(string filename, System.Action<string> mergeAction)
        {
            string baseFile = Path.FindFile(filename);
            if (!string.IsNullOrEmpty(baseFile))
            {
                mergeAction(baseFile);
                return;
            }
            ...
            for (int i = 0; i < mEnabledModList.Count; i++)
            {
                Mod mod = mEnabledModList[i];
                string destFile = mod.GetFullPath(filename);
                if (File.Exists(destFile))
                {
                    mergeAction(destFile);
                    return;
                }
            }
        }
```

**内容根目录优先，命中即返回，不做多份合并。** 因此：

- 内容根目录和 Mod 目录都有同名文件时，**Mod 那份永远不会被读**。
- 只改 Mod 副本而内容根目录也有同名文件 → 改动完全无效，且没有任何日志提示。

排查「改了不生效」时，第一件事就是确认内容根目录有没有同名文件。

---

## 加载时机与缓存

- 链路 B/C 都有静态 `loaded` 标志（`CombatExpTable.loaded`、`GainValueConfig.jsonDefaultsLoaded`），**同一次运行内只读一次**，改文件必须重启。
- `Path.ContentRootPath` 为空时（编辑器里直接调用 / 游戏启动早期），`CombatExpTable.EnsureLoaded()` **不置 `loaded`**，只返回内置表，等 Path 就绪后重读；`GainValueConfig` 则是直接 `return` 不置位。
- 链路 A 依赖 `GameData.ScenarioCommonData` 是否已构建（`LoadCommonData` 首行判空返回）。

## 日志锚点

改完重启游戏，看控制台确认命中了哪份文件（日志一律中文，级别 `Info` / `Warning` / `Error`）：

```
CombatExpTable 已从 Data/Common/CombatExpTable.json 载入
未找到 Data/Common/CombatExpTable.json，使用内置原版 Exp 表
GainValueConfig 默认值已从 Data/Common/GainValueConfig.json 载入（N 项）
GainValueConfig 的 <表名> 出现未知键 <key>，已忽略
Data/Common/CombatExpTable.json 缺少 entries 数组，沿用内置表
```

后两条 `Warning` 是「字段名写错」的直接证据——配置表里的键名拼错不会抛异常，只会静默失效。
