# Unity 侧：编译验证与代码动态 UI

## 1. 编译验证流程

### 1.1 刷新 + 编译

```
refresh_unity(mode="force", scope="all", compile="request", wait_for_ready=true)
```

**新增 `.cs` 文件必须 `scope="all"`**。
`scope="scripts"` 只重编已在 AssetDatabase 里的脚本，新文件不会生成 `.meta`、不会进程序集，
报错表现是"我明明写了这个类，却提示类型不存在"。

判断文件是否真的被 Unity 接管：

```powershell
powershell -NoProfile -Command "Get-ChildItem '路径' -Filter 'Xxx*' | Select-Object Name"
```

没有 `.meta` = 还没被导入，继续用 `scope="all"` 刷新。

### 1.2 读日志

```
read_console(action="get", types=["error"], count=25, format="plain")
```

### 1.3 兜底（read_console 超时 / ping 不通）

MCP 桥接偶发超时，此时不要下"没错误"的结论，改用磁盘证据：

```powershell
powershell -NoProfile -Command "Get-Content 'C:\Users\Administrator\AppData\Local\Unity\Editor\Editor.log' -Tail 500 | Select-String 'error CS'"
powershell -NoProfile -Command "Get-Item 'd:\sango_infinity\Project\Library\ScriptAssemblies\Assembly-CSharp.dll' | Select-Object LastWriteTime; Get-Date -Format 'HH:mm:ss'"
```

判读：
- `Editor.log` 尾部无 `error CS` + 程序集时间戳在几十秒内 → 编译通过
- 程序集时间戳没变 → 编译没触发或失败，不要宣称通过

**PowerShell 注意**：命令行里的 `$` 会被外层 shell 吞掉，`$var` 会变成空。避免用变量，或把脚本写成 `.ps1`/`.py` 文件再执行。

---

## 2. 纯代码动态 UI 的扩展模式

`UIScenarioVariables` 这类界面**完全由代码生成**：每种控件在 prefab 上有一个模板对象
（`titleObj` / `integerObj` / `dropdownObj` / `toggleObj` …），`AddXxxItem()` 里 `Instantiate` 模板、
`Set()` 填值、`itemList` 记账，`RemoveItem()` 按 `obj.name` 回收到对应池。

### 2.1 加一种控件而不改 prefab

复用排版最接近的模板（通常是 `titleObj`），额外挂组件：

```csharp
GameObject obj = GameObject.Instantiate(titleObj, titleObj.transform.parent);
obj.name = "buttonObj";                       // 名字决定回收进哪个池
UnityEngine.UI.Image img = obj.AddComponent<UnityEngine.UI.Image>();
img.color = new Color(0.16f, 0.42f, 0.78f, 0.55f);
UnityEngine.UI.Button btn = obj.AddComponent<UnityEngine.UI.Button>();
btn.targetGraphic = img;
btn.onClick.RemoveAllListeners();             // 复用对象必须清旧监听
btn.onClick.AddListener(() => onClick?.Invoke());
```

### 2.2 三条硬性约束

1. **必须单独成池**。带 `Button` / `InputField` 的对象如果回流到 `pool_titleObj`，下次当普通标题行复用时
   会残留监听（点标题就触发保存）。新增 `pool_buttonObj` / `pool_inputObj`，并在 `RemoveItem` 的
   `switch (item.name)` 里加对应 `case`。
2. **`InputField` 必须先失活再配 `textComponent`**。`AddComponent<InputField>()` 会立刻 `OnEnable →
   UpdateLabel()`，此时 textComponent 为 null 会报空引用。流程：`Instantiate → SetActive(false) → 配属性 → SetActive(true)`。
3. **文本组件优先取显式绑定**：`UITitleField.title` 拿不到再退回 `GetComponentInChildren<Text>(true)`。

### 2.3 刷新会重建整页，状态要存字段

`RefreshSetting()` 会回收所有 item 再重建，输入框内容也会丢。
需要跨刷新的值（如"当前输入的预设名"）必须存在 MonoBehaviour 字段上，重建时回填。

### 2.4 接口扩展要确认实现者数量

`IVariablesSetting` 只由 `UIScenarioVariables` 实现。加接口方法前先 `search_content "IVariablesSetting"`
确认没有第二实现者，否则会连带编译失败。

---

## 3. 三源数据管理器模式（内置 / 自建 / 模组）

新做"可扩展的内容库"（事件、预设、队伍、地图资源）时，照抄这个结构：

```
内置：{Path.ContentRootPath}/Data/{Folder}/**/*.json
自建：{Path.CustomEditRootPath}/Data/{Folder}/**/*.json      ← 保存目标
模组：{Path.ModRootPath}/**/Data/{Folder}/**/*.json          ← 必须校验路径片段
```

要点：
- **加载顺序即优先级**，同名后者覆盖前者（内置 → 自建 → Mod）
- 模组是递归扫整棵树，必须用 `normalized.IndexOf("/Data/{Folder}/") >= 0` 过滤，否则会把模组里其它 JSON 当数据读
- **只写自建目录**，模组/内置一律只读；删除时先判断文件是否存在，不存在就明确报"来自 Mod，无法删除"
- 文件名即 ID/名字；保存前用 `SanitizeName` 过滤 `Path.GetInvalidFileNameChars()` 与 `/ \`，防路径穿越
- 保存后调 `Reload()` 让新文件立刻出现在列表里

参考实现：
- `Game/Event/EventDatabase.cs`（三源 + 索引）
- `Game/Scenario/ScenarioVariablesPresetManager.cs`（三源 + 保存/删除）
- `Game/Scenario/Scenario.cs:1179`（剧本目录扫描）

## 4. 剧本侧参数落地

给剧本加"默认引用某个预设"这类字段时：
- 加到 `ScenarioInfo`（`[JsonProperty]`，缺省 null/空 = 不启用），会随剧本 JSON 与存档一起走
- 读取时机放在 `Scenario.LoadVariables()` 末尾 —— 选剧本界面与进入参数界面都会走它
- 用 `Scenario.LoadVariables()` 只在 `Variables == null` 时才被调用的特性，保证**玩家本次的修改不会被二次覆盖**
