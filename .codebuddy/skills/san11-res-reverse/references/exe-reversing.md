# san11pk.exe 静态与运行时逆向手法

工具都在 `d:\sango_infinity_tools\San11Res\`，全部是临时脚本（`_` 前缀），用完可删；
`_dbg_probe.py` 是运行时分析的唯一入口，值得留着。

---

## 1. PE 骨架（先记住，能省掉一半「地址对不上」的困惑）

```
映像基址 0x00400000   段数 6
  .text    VA 0x00401000 - 0x0074DF19   raw_off 0x00001000
  .rdata   VA 0x0074E000 - 0x0089C12D   raw_off 0x0034E000
  .data    VA 0x0089D000 - 0x09C617E0   raw_off 0x0049D000   ← 154 MB，绝大部分是 0
  .rsrc    VA 0x09C62000 - 0x09C7BECE
  stxt774  VA 0x09C7C000 - 0x09C7E08F
  stxt371  VA 0x09C7F000 - 0x09C82580
```

**最重要的一条**：`.data` 是游戏的**静态池**，模型/动画/实体对象全在
`0x009xxxxx` – `0x09Cxxxxx` 这段里，**不是 malloc 出来的**。
所以看到 `0x09415A20` 这类「看着像堆」的地址，先查段表 —— 它在映像内。
反过来说，`.data` 文件内容几乎全是 0，**运行时才写进去的表，静态永远搜不到**。

---

## 2. 找引用：三级武器，按成本递增

| 级别 | 手段 | 适用 |
|---|---|---|
| 1 | `_exe_xref.py`（字符串 → 代码） | 已知字符串（路径、`BNK_*`、格式名） |
| 2 | `_exe_ptrfind.py 0xVA`（找指向 VA 的 4 字节指针） | 找 `.data/.rdata` 里的描述符表、函数表、vtable |
| 3 | `_dbg_probe.py --guard` | 运行时才生成的表 / 动态写入点 |

对「某个全局变量被谁写」这类问题，**只有第 3 级有效**。

---

## 3. 反汇编（capstone）的两个坑

1. **从任意偏移起步会静默错位。** 只要起点不在指令边界，capstone 会一路解出垃圾但**不报错**
   （例：`0x44B600` 处解出 `push es`）。正确做法：先用 `_exe_fn.py` 从 `int3` 填充之后起步，
   或先 `--from` 前推几百字节肉眼核对指令流是否连贯。
2. **`md.disasm` 遇到无法解码的字节会直接停**，全段扫描必须 `md.skipdata = True`，
   否则「9680 条指令」就以为是全段（实际 `.text` 有 3.4 MB）。

**搜索代替反汇编**：如果只是想知道「某个 4 字节常量在哪儿被引用」，直接
`data.find(struct.pack("<I", va))` 全文件搜，比反汇编全段又快又不漏（含 `.data` 里的指针表）。
注意 x86 也会用「基址 + 偏移」形式，搜不到时换 `va - 基址` 再试一次。

---

## 4. 运行时探针 `_dbg_probe.py`

原理：`CreateProcessW(DEBUG_ONLY_THIS_PROCESS)` 拉起（或 `--attach` 附加到）32 位游戏，
写 `INT3`、`WaitForDebugEvent`、读 WOW64 线程上下文，命中后 dump 参数/栈/指针内存，
再单步恢复原字节重新上断。

### 4.1 基本用法

```bash
# 新拉起游戏，在 0x44ABA0 抓 20 次命中，120 秒超时
python _dbg_probe.py --bp 0x44ABA0 --max-hits 20 --timeout 120

# 附加到已经跑到目标画面的进程（不会杀进程）
python _dbg_probe.py --attach 30124 --bp 0x44ABA0 --max-hits 40

# 只抓第一次
python _dbg_probe.py --bp 0x46E470 --oneshot
```

日志写 `out/_dbg_log.txt`（UTF-8）。

**只读探针**：只是想看某个地址当前是什么，不必断点 —— `_peek_slots.py` 用
`OpenProcess(PROCESS_VM_READ)` + `ReadProcessMemory` 直接读，不改内存、不挂调试器：

```bash
python _peek_slots.py                                   # 自动找 san11pk.exe，读默认 0x9CF5F0
python _peek_slots.py --addr 0x9CF5F0 --span 0x200      # 按 16 字节一槽解析，自动匹配归档号
python _peek_slots.py --scan 0x9CF000 0x9D0000          # 扫区间里所有「指向 FCVD 的指针」
```

游戏必须已经在目标画面（单挑/舌战），否则那块内存是 0。

**实践要点**：探针只负责「等」，不会帮你玩。想抓战场/单挑里的对象，
要么 `--attach` 到已经进到那个画面进程，要么新拉起后在 `--timeout` 内手动点进去。
不操作就只能抓到启动期噪声（用 `--after N` 跳过前 N 秒）。

### 4.2 参数速查

| 参数 | 作用 |
|---|---|
| `--bp 0xVA`（可多次） | 断点。**默认值是 `0x46E470,0x44D4C0`**，不传就等于抓归档取数 |
| `--oneshot` / `--no-rearm` | 每断点只抓一次 / 命中后不重新上断 |
| `--after N` | 启动后 N 秒才开始记录（跳过启动噪声） |
| `--min-arg N` | `[esp+4] < N` 的命中不记录（过滤 UI 小缓冲；模型类用 1024） |
| `--caller-skip 0x4D1165,...` | 调用者黑名单 |
| `--fcv 帧数:块数` | 只记 `arg2` 是 FCVD 且头匹配的命中（如 `--fcv 31:147`） |
| `--idx-lo/--idx-hi/--idx-off` | 按「资源号/归档号」过滤，`--idx-off 12` 对应 ArchiveRead、`4` 对应 GetResource |
| `--peek 0x...` | **命中时**额外 dump 绝对地址（逗号分隔） |
| `--reg ECX --reg-off 0x5C --reg-len 192` | dump 某寄存器指向的内存 |
| `--reg-nz 0x5c` | 只在 `[reg+0x5c] != 0` 时记录（筛「挂上了动作对象」的模型） |
| `--reg-eq ESI:0x09143870` | 只在寄存器等于某值时记录 |
| `--chain "ESI:0x58,0x24"` | 链式解引用：从 ESI 出发逐级取 `[cur+off]` |
| `--rel-dump "0x3C:0x50"` | 链尾：读 `[cur+0x3C]` 当偏移，dump 0x50 字节 |
| `--chain-dump 256` | 链尾绝对 dump N 字节（跨帧比对调色板用） |
| `--sample 5 --sample-wait 10` | 定期挂起主线程读 EIP 做热点统计（找渲染循环） |
| `--guard "ECX:0x50" --guard-len 256` | 给 `[ECX+0x50]` 指向的页加 `PAGE_GUARD`，抓**第一个访问者** EIP |

### 4.3 WOW64 的关键限制

- **硬件写断点不生效**：`Dr0~Dr3` 经 `Wow64SetThreadContext` 写不进去，`--watch` 是保留参数。
- 替代方案是 `PAGE_GUARD`：给目标页置 guard，任何读写都会抛 `STATUS_GUARD_PAGE`，
  命中时 `EIP` 就是访问者。代价是被监视的页会变慢，**只在目标页上开，且抓到一次就关**。
- 32 位断点/单步在 WOW64 下上报的状态码是 `0x4000001F` / `0x4000001E`，
  **不是** `0x80000003` / `0x80000004`；按后者判断会漏掉全部命中。
- 读上下文要用 `Wow64GetThreadContext` + 716 字节的 `WOW64_CONTEXT`（偏移见脚本常量）。

---

## 5. 实战：定位「运行时的动作槽」

1. 断点下在**疑似装载动作的位置**，命中后看 `[esp+4..]` 与寄存器。
2. 发现某个指针指向 `FCVD0022` —— 那就是加载到内存的 fcv 副本（`0x10` 起已是绝对指针）。
3. 用 `--chain` 顺着对象往上走（`ESI:0x58,0x24` → `+0x3C` 得到数据区基址），
   确认到底是「谁持有这张表」。
4. 拿槽里的 `(骨骼数, 帧数)` 回归档里做**唯一匹配**（`1716` 的 `147 块 / 21 骨骼 / 31 帧`），
   两边对上才算定案。

**失败案例（务必避开）**：直接把 `0x009CF5F0` 当「静态动作表」在 exe 里搜立即数 —— 0 命中，
因为它在 `.data` 的零填充区，运行时才写。另一处把断点下在 `0x0044ABA0`，
那其实是骨骼矩阵搬运循环的循环体：

```
0x44ABA0  mov eax, [esi+0x58]      ; 源骨骼矩阵数组
0x44ABA6  mov ebp, [eax+0x3c]
0x44ABAE  movzx eax, word [eax+ebp+2]   ; u16 骨骼索引
0x44ABB3  shl eax, 6                    ; ×64 = 矩阵大小
0x44ABC7  add edi, 0x28                 ; 下一根的 40 字节记录
0x44ABCA  add ebx, 0x40                 ; 目标矩阵 64 字节
```

命中时 `EBP` 会显示成 `0x009CF5F0` 这种「像指针的数据」，很容易误判成「动作表头」。

---

## 6. 从 exe 恢复资源路径（`map_from_exe.py` 的原理）

1. `sango_names_from_exe.py` 扫 `.data` 的字符串指针数组（如 `0x49e098..0x49eed4`），
   取出全部 `media/...` 路径 → `exe_paths.json`。
2. `map_from_exe.py` 按数组顺序把路径铺到归档块上，**逐块校验**
   「块内该 magic 的条数 == exe 该目录唯一路径数」，全过才写 `index_map.json`。
3. 少数目录顺序是反的，需要人工锚点纠正（`ANCHOR_NAME_ORDER`），
   锚点见 `references/formats.md` §8。

命中率不是 100% 是正常的：归档里有 exe 根本没有路径的资源（武将立绘、AIMG、KOVS、
PK 追加贴图），这些只能按 magic + 相邻关系推断用途。

---

## 7. 判断「资源驱动 vs 程序化」

问「这个动作/效果是怎么来的」时，先查归档里有没有对应资源，再决定要不要去反汇编：

- 有资源 → 按格式解析即可，别去 exe 里找（`WKMD`/`FCVD`/`AIMG` 就是这条路）。
- 没资源 → 一定是程序化。典型例子：战场部队（`UNI00`…`UNI16`）在归档里
  **没有任何 fcv**，所以它们的战场表现只能来自整模型变换，不存在骨骼动画数据。

不要因为「游戏里看着有动画」就假定存在动画文件 —— 这是本项目最容易走偏的假设。
