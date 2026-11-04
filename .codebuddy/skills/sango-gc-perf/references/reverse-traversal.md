# SangoObjectList 必须倒序遍历

## 一句话

`SangoObjectList<T>` 底层是 `List<T>`：**删除会让后面的元素前移一位**。循环体里只要可能移除元素，遍历就必须从 `Count - 1` 往 `0` 走；正序会静默跳过紧邻的下一个元素——不报错、不崩，只是那个人/那座建筑没被处理。

## 两个容器的遍历方向是相反的，别记混

| 容器 | 底层 | 删除行为 | 正确方向 | 出处 |
|---|---|---|---|---|
| `SangoObjectList<T>` | `List<T>` | 元素前移，Count 变小 | **倒序** `Count-1 → 0` | `Game/Object/Core/SangoObjectList.cs:130-148` |
| `SangoObjectSet<T>` | `T[]`（按 Id 索引，允许空位） | 置空，不压缩 | **正序** `1 → Length-1`（下标 0 保留） | `Game/Object/Core/SangoObjectSet.cs:130-148` |

`SangoObjectList.ForEach` 的两个重载本来就是倒序实现的：

```132:148:Project/Assets/Sango/Scripts/Game/Object/Core/SangoObjectList.cs
        public override void ForEach(Action<T> action)
        {
            for (int i = mObjects.Count - 1; i >= 0; i--)
            {
                T obj = mObjects[i];
                if (obj != null)
                    action(obj);
            }
        }
```

所以**用 `ForEach(lambda)` 的历史代码是安全的**；把 `ForEach` 改写成 `for` 时若写成正序，就是行为回归——这是本项目最容易踩的一类优化事故。

## 真实事故：夏口港城陷只迁走了诸葛亮

`City.cs` 城陷迁出武将段（原 `ForEach(lambda)` 被改写为正序 `for`）：

- 循环体 `person.ChangeBelongCity(escapeCity)` 会经 `PersonStateSystem` 走到 `City.RemovePerson`，把当前武将**从 `allPersons` 里移除**，后面所有人前移一位；
- 正序 `i++` 于是直接跳过了前移上来的下一个人；
- 表现：同城多名武将只有第一个被处理，其余留在已易主的城里，**没有归属、没有编制**，直到回合结束的归属自愈才被捞回来。

原注释保留了复盘（`City.cs:2806-2817`）：

```2809:2816:Project/Assets/Sango/Scripts/Game/Object/City/City.cs
            // 【必须倒序 —— 血泪教训】
            // 循环体内的 person.ChangeBelongCity(escapeCity) 会走到 City.RemovePerson，
            // 把当前武将**从 allPersons 里移除**，后面的元素整体前移一位。
            // 正序遍历时 i++ 会直接跳过被前移上来的下一个人 ——
            // 表现就是"同城多名武将只有第一个被处理，其余留在已易主的城里无人管"
            // （实测：夏口港陷落时只处理了诸葛亮，关羽被跳过，直到回合结束才被归属自愈捞回来）。
            // SangoObjectList.ForEach 本身是**倒序**（Count-1 → 0）遍历，正好躲开这个问题；
            // 改写成 for 时必须显式保持倒序，否则就是行为回归。
```

## 同类现场（都是倒序，改动时不要"顺手纠正"成正序）

| 位置 | 循环体里的移除动作 | 后果 |
|---|---|---|
| `City.cs:1985-1988` | `building.OnFall(null)` → `City.OnBuildingDestroy` 从 `allBuildings` 摘除 | 正序会跳过下一座建筑，黑市拆不干净 |
| `City.cs:2772-2778` | `LeaveToWild` / `building.OnFall` / `Escape` 各自摘元素 | 三个列表同时漏项 |
| `City.cs:3091-3094` | `PersonStateSystem.Recruit` 把俘虏从 `captiveList` 摘掉 | 俘虏只招到第一个 |
| `MapTerrain.cs:256-262` | 解登记的块就地 `RemoveAt(i)` | 帧更新列表残留已销毁块 |
| `ScenarioEventManager.cs:1893-1899` | 过期事件 `RemoveAt(i)` | 定时事件漏删，重复触发 |
| `Editor/DuelWindowBuilder.cs:330-334` | `UnityEvent` 持久监听逐个移除 | 漏扫监听，死绑定检测失真 |

## 正确 / 错误写法

```csharp
// ✅ 倒序：移除当前元素不影响尚未遍历的部分
for (int i = list.Count - 1; i >= 0; i--)
{
    Person p = list[i];
    if (p == null) continue;          // 空位跳过，不要 RemoveAt 后再 i--（易写错）
    if (p.IsDead) { p.LeaveCity(); continue; }   // LeaveCity 内部会 Remove
}

// ❌ 正序 + 移除：紧邻的下一个元素被跳过，且不会报错
for (int i = 0; i < list.Count; i++)
{
    Person p = list[i];
    if (p.IsDead) p.LeaveCity();      // list 变短，i 已经指向了"下下个"
}
```

### 另一种合法写法：快照

当循环体改动的是**多个相关列表**、或需要正序语义（比如按优先级先到先得）时，先快照再处理，不要边遍历边改源列表：

```csharp
// 出处：Troop.cs:3665-3668 —— RemovePerson 会把副将2前移到副将1的位置
Person[] members = new Person[] { Member1, Member2 };
for (int i = 0; i < members.Length; i++) { /* 逐个摘人 */ }
```

```csharp
// 出处：DiplomacyActionAnnex.cs:269-271 —— Recruit 会改 Receiver 名册
List<Person> members = new List<Person>();
Receiver.ForEachPerson(p => { if (p != null && p.IsAlive) members.Add(p); });
```

快照会分配一次数组/List，**只在非热路径或列表很短时用**；热路径仍应倒序原地遍历。

## 反模式：`移除后 i--`

`ForceAI.cs:1634-1637` 记录了这种写法：

```1634:1637:Project/Assets/Sango/Scripts/Game/Object/Force/ForceAI.cs
            // 【注意】循环内统一采用"移除后 i--"的写法：因为删除当前元素后，
            // 后续元素会前移填补该下标，i-- 再配合循环的 i++ 即可回到同一位置继续检查。
            // 该写法成立的前提是**列表确实缩短了** —— 因此凡是要 i-- 的分支，
            // 都必须先把元素从列表中真正移除，否则会陷入死循环。
```

它成立但**极其脆弱**：任何一条 `i--` 分支忘了真正移除元素，就是死循环卡死。新代码优先用倒序，不要新增这种写法。

## 迁移检查清单（把 ForEach/lambda 改写成 for 时逐条过）

1. 容器是 `SangoObjectList`（倒序）还是 `SangoObjectSet`（正序 1..Length）？数组看是不是定长索引表。
2. 循环体有没有可能移除元素？调用链里是否间接调到 `Remove` / `RemovePerson` / `RemoveBuilding` / `RemoveAt` / `Die` / `OnFall` / `LeaveCity` / `Recruit` / `Escape`？有 → 必须倒序。
3. lambda 里的 `return` 是否都改成了 `continue`？有没有误写成 `break`？
4. 原来是 `Find`（返回第一个）还是倒序扫描（返回最后一个）？语义要保持。
5. 空列表、`null` 元素、Count 在循环中变化这三点是否都和原实现一致？
6. 换重载时默认参数是否补齐（如 `Hex.Ring` 的 `startDir = 3`）？边界值（`radius == 0`）行为是否一致？
7. 改完是否在注释里写明「必须倒序」及原因？下一个改这段的人不会替你记住。
