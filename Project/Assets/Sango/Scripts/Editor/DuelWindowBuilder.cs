/*
 * 文件名：DuelWindowBuilder.cs
 * 描述：单挑界面 window_duel.prefab 的编辑器重排工具
 *
 * 目标：把单挑界面的元素摆放成《三国志11》原版单挑界面的样子：
 *   · 左上 / 右上：双方武将头像 + 姓名 + 武力 + 体力条 + 斗志条（右侧整体镜像）
 *   · 顶部中央   ：合数
 *   · 中央       ：卡牌展示区（原版此位置是两名武将骑马对砍，这里换成左右卡牌对垒）
 *   · 左下方     ：四条行动方针斜向排列（重视攻击 → 重视防守 → 重视斗志 → 重视一发）
 *   · 左下角     ：秘杀（必杀）　　下方中央：停止　　最下方：可对武将下达指示
 *   · 右侧       ：交替（换将）按钮
 *   · 右下角     ：战报日志（本项目新增，用于卡牌表现的文字反馈）
 *
 * 重要约定：
 *   1. 只调整 RectTransform 的位置 / 尺寸与 Text 的内容，**不覆盖任何已有 Image 的 sprite 与颜色**，
 *      找不到对应精灵图的元素一律保持原样（仅摆位置），避免把美术资源改坏。
 *   2. 所有坐标以 1920×1080 为参考分辨率，配合锚点定位，在其它分辨率下等比缩放。
 *   3. 缺少的容器节点（卡牌区等）会新建，新建节点不带美术，仅作为占位与布局用。
 *
 * 用法：菜单 Sango/单挑/重排单挑界面，或由 MCP 反射调用 Build()。
 */

using Sango.Core.Duel;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.EditorTools
{
    /// <summary>单挑界面排版工具</summary>
    public static class DuelWindowBuilder
    {
        private const string PrefabPath = "Assets/Mods/Content/Assets/UI/Prefab/window_duel.prefab";

        /// <summary>参考分辨率（设计坐标基准）</summary>
        private static readonly Vector2 RefResolution = new Vector2(1920f, 1080f);

        #region 入口

        [MenuItem("Sango/单挑/重排单挑界面")]
        public static void BuildFromMenu()
        {
            Build();
        }

        /// <summary>
        /// 只重新绑定节点引用（不动任何排版）。
        /// 美术手工摆过 prefab 之后用它把引用写回去，不会被重排覆盖。
        /// </summary>
        [MenuItem("Sango/单挑/仅重绑节点引用")]
        public static void BindOnlyFromMenu()
        {
            BindOnly();
        }

        /// <summary>只做绑定：把 CardDuelView 的引用与合数数字图写入 prefab</summary>
        public static string BindOnly()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null) return "prefab not found: " + PrefabPath;

            try
            {
                string bindMsg = BindDuelView(root.transform);
                string digitMsg = BindBlowCounterDigits(root.transform);
                // 必杀按钮的两态由 Button 的 interactable 表达，不再需要绑三态图
                string deadMsg = SweepDeadListeners(root.transform);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                PrefabUtility.UnloadPrefabContents(root);
                AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
                return "仅绑定 OK；" + bindMsg + "；" + digitMsg + "；" + deadMsg;
            }
            catch (System.Exception e)
            {
                PrefabUtility.UnloadPrefabContents(root);
                return "bind failed: " + e.Message;
            }
        }

        /// <summary>
        /// 把 4849-1 图集里的 0~9 数字图填进 CardDuelView.blowCounterDigits，
        /// 供「十位 / 个位」两张图片按合数换图。
        /// </summary>
        private static string BindBlowCounterDigits(Transform root)
        {
            CardDuelView view = root != null ? root.GetComponent<CardDuelView>() : null;
            if (view == null) return "合数数字图：没有 CardDuelView";

            if (view.blowCounterDigits == null || view.blowCounterDigits.Length < 10)
                view.blowCounterDigits = new UnityEngine.Sprite[10];

            int found = 0;
            for (int d = 0; d < 10; d++)
            {
                UnityEngine.Sprite sp = LoadAtlasSprite("4849-1_" + d);
                if (sp != null) found++;
                // 找不到也照写，保留已有的引用
                if (sp != null || view.blowCounterDigits[d] == null)
                    view.blowCounterDigits[d] = sp;
            }

            return "合数数字图 " + found + "/10";
        }

        /// <summary>从 4849-1 图集里取一张图（合数数字图用）</summary>
        private static UnityEngine.Sprite LoadAtlasSprite(string name)
        {
            return AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(
                "Assets/Mods/Content/Assets/UI/AtlasTexture/4849-1/" + name + ".png");
        }

        /// <summary>
        /// 重建单挑界面 prefab。
        ///
        /// 现在只做三件事：清理历史残留节点、把 CardDuelView 的引用与合数数字图写进 prefab、
        /// 把空头像占位置透明。界面的位置 / 大小 / 美术图全部由美术手工摆放，脚本一律不动，
        /// 免得每次重建都把手工稿覆盖回旧稿的样子。
        /// </summary>
        public static string Build()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null) return "prefab not found: " + PrefabPath;

            try
            {
                Transform t = root.transform;

                // 清理历史残留节点（占位进度条、旧合数 / 提示 / 日志 / 标题）
                string cleanMsg = DuelWindowCleanup.Run(root);

                // 把 CardDuelView 的节点引用固化进 prefab（否则 Inspector 里看全是空的），
                // 并补上合数用的十张数字图（运行时按 BlowCounter 取用）
                string bindMsg = BindDuelView(t);
                string digitMsg = BindBlowCounterDigits(t);

                // 收尾只剩"把空头像占位置透明"这一项，其余都不碰
                string polishMsg = DuelWindowCleanup.Polish(root);

                // 顺手扫掉指向已不存在方法的持久化回调（重构后残留的死绑定）
                string deadMsg = SweepDeadListeners(t);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                PrefabUtility.UnloadPrefabContents(root);
                AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
                return "window_duel 已重建；" + cleanMsg + "；" + bindMsg + "；" + digitMsg + "；" + polishMsg
                    + "；" + deadMsg;
            }
            catch (System.Exception e)
            {
                PrefabUtility.UnloadPrefabContents(root);
                return "build failed: " + e.Message;
            }
        }

        #endregion

        #region 查找工具

        /// <summary>递归按名字查找</summary>
        private static Transform Deep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform t = Deep(root.GetChild(i), name);
                if (t != null) return t;
            }
            return null;
        }

        #endregion

        /// <summary>
        /// 把 CardDuelView 的节点引用固化进 prefab。
        ///
        /// 该 prefab 原先一条引用都没写，全靠运行时 Awake() 里的名字查找，
        /// 结果 Inspector 里看全是 None，像是"没绑定"；运行时一旦查找失败就毫无表现。
        /// 这里显式绑定一次并随 prefab 保存，同时补齐项目其它窗口都有的 UGUIWindow。
        /// </summary>
        private static string BindDuelView(Transform root)
        {
            if (root == null) return "绑定：根节点为空";

            CardDuelView view = root.GetComponent<CardDuelView>();
            bool created = false;
            if (view == null)
            {
                view = root.gameObject.AddComponent<CardDuelView>();
                created = true;
            }

            // 按名字查找并写入序列化字段
            view.Rebind();

            // CardDuelView 本身就是 UGUIWindow，根节点上不应再有第二个（空的）UGUIWindow：
            // 那会让 Window.CreateWindow 取到空壳而不是表现层本体。这里顺手清掉历史遗留。
            UGUIWindow[] wins = root.GetComponents<UGUIWindow>();
            for (int i = 0; i < wins.Length; i++)
            {
                if (wins[i] != null && !(wins[i] is CardDuelView))
                    UnityEngine.Object.DestroyImmediate(wins[i]);
            }

            int bound = 0;
            int total = 0;
            Count(view.blowCounterText, ref bound, ref total);
            Count(view.logText, ref bound, ref total);
            Count(view.btnPlay, ref bound, ref total);
            Count(view.skipHint, ref bound, ref total);
            Count(view.leftCharaSlots, ref bound, ref total);
            Count(view.rightCharaSlots, ref bound, ref total);
            Count(view.leftSpiritPips, ref bound, ref total);
            Count(view.rightSpiritPips, ref bound, ref total);
            CountSide(view.leftSide, ref bound, ref total);
            CountSide(view.rightSide, ref bound, ref total);

            string buttonMsg = BindButtonEvents(root, view);

            return "绑定 CardDuelView " + bound + "/" + total + " 项"
                + (created ? "（新建组件）" : "") + "；" + buttonMsg;
        }

        /// <summary>统计一套命令区里绑上了多少项</summary>
        private static void CountSide(CardDuelView.DuelCommandSide side, ref int bound, ref int total)
        {
            if (side == null) return;

            Count(side.root, ref bound, ref total);
            Count(side.specialBg, ref bound, ref total);
            Count(side.btnSpecial, ref bound, ref total);
            Count(side.stanceGroup, ref bound, ref total);
            Count(side.stanceToggleGroup, ref bound, ref total);
            Count(side.stanceToggles, ref bound, ref total);
            Count(side.specialPanel, ref bound, ref total);
            Count(side.specialCells, ref bound, ref total);
            Count(side.switchButtons, ref bound, ref total);
        }

        /// <summary>
        /// 把按钮的 onClick 持久化接到 CardDuelView 的公开回调上。
        /// 这样 Inspector 里能看到完整的事件绑定，也不再依赖运行时 AddListener。
        /// </summary>
        private static string BindButtonEvents(Transform root, CardDuelView view)
        {
            if (view == null) return "按钮事件：没有 CardDuelView";

            int count = 0;

            // 底部四态合一的那一个按钮（决定 / 停止 / 终止 / 替换）
            count += Wire(root, "BtnPlayPauseSwitch", view, "OnPlayClick");

            // 左右两套必杀技按钮各接一个。它们同名（都叫 BtnSpecial），所以按引用接线，不能按名字找。
            // 方针是 Toggle、必杀格与交替按钮需要知道自己在哪一侧，这三类由运行时 BindButtons 挂。
            count += Wire(view.leftSide != null && view.leftSide.btnSpecial != null
                ? view.leftSide.btnSpecial.transform : null, view, "OnLeftSpecialClick");
            count += Wire(view.rightSide != null && view.rightSide.btnSpecial != null
                ? view.rightSide.btnSpecial.transform : null, view, "OnRightSpecialClick");

            return "按钮事件 " + count + " 个";
        }

        /// <summary>把指定按钮的 onClick 接到 view 上的一个无参方法（按引用接线，同名节点也能区分）</summary>
        private static int Wire(Transform buttonTransform, CardDuelView view, string methodName)
        {
            if (buttonTransform == null || view == null) return 0;

            Button button = buttonTransform.GetComponent<Button>();
            if (button == null) return 0;

            System.Reflection.MethodInfo method = typeof(CardDuelView).GetMethod(methodName);
            if (method == null) return 0;

            UnityEngine.Events.UnityAction action = System.Delegate.CreateDelegate(
                typeof(UnityEngine.Events.UnityAction), view, method) as UnityEngine.Events.UnityAction;
            if (action == null) return 0;

            // 先清掉旧的持久化监听，避免重复挂导致一次点击触发多次
            while (button.onClick.GetPersistentEventCount() > 0)
                UnityEditor.Events.UnityEventTools.RemovePersistentListener(button.onClick, 0);

            UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, action);
            return 1;
        }

        /// <summary>把某个按钮的 onClick 接到 view 上的一个无参方法</summary>
        private static int Wire(Transform root, string buttonName, CardDuelView view, string methodName)
        {
            Transform t = Deep(root, buttonName);
            if (t == null) return 0;

            Button button = t.GetComponent<Button>();
            if (button == null) return 0;

            System.Reflection.MethodInfo method = typeof(CardDuelView).GetMethod(methodName);
            if (method == null) return 0;

            UnityEngine.Events.UnityAction action = System.Delegate.CreateDelegate(
                typeof(UnityEngine.Events.UnityAction), view, method) as UnityEngine.Events.UnityAction;
            if (action == null) return 0;

            // 先清掉旧的持久化监听，避免重复挂导致一次点击触发多次
            while (button.onClick.GetPersistentEventCount() > 0)
                UnityEditor.Events.UnityEventTools.RemovePersistentListener(button.onClick, 0);

            UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, action);
            return 1;
        }

        /// <summary>
        /// 扫掉按钮上指向"已经不存在的方法"的持久化回调。
        ///
        /// 回调改名（例如交替按钮以前叫 OnSwitch0Click）之后，旧 prefab 里会留下死绑定：
        /// Inspector 上看着有接线，运行时却找不到方法、点下去毫无反应；
        /// 更麻烦的是 CardDuelView 会因此认为"这个按钮已经接好了"而跳过运行时绑定。
        /// 这里统一清掉，让它回到由运行时 BindButtons 挂监听的状态。
        /// </summary>
        private static string SweepDeadListeners(Transform root)
        {
            if (root == null) return "死绑定：0 处";

            int removed = 0;
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                // 倒着删，避免删掉一条后后面的下标整体前移而漏扫
                for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                {
                    if (IsLiveListener(button.onClick.GetPersistentTarget(i),
                                       button.onClick.GetPersistentMethodName(i)))
                        continue;

                    UnityEditor.Events.UnityEventTools.RemovePersistentListener(button.onClick, i);
                    removed++;
                }
            }
            return "死绑定：" + removed + " 处";
        }

        /// <summary>持久化回调是否指向真实存在的无参方法</summary>
        private static bool IsLiveListener(UnityEngine.Object target, string method)
        {
            if (target == null || string.IsNullOrEmpty(method)) return false;

            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance;

            return target.GetType().GetMethod(method, flags, null, System.Type.EmptyTypes, null) != null;
        }

        /// <summary>统计单个引用是否绑定成功</summary>
        private static void Count(UnityEngine.Object target, ref int bound, ref int total)
        {
            total++;
            if (target != null) bound++;
        }

        /// <summary>统计一组引用是否绑定成功</summary>
        private static void Count(UnityEngine.Object[] targets, ref int bound, ref int total)
        {
            if (targets == null) return;
            for (int i = 0; i < targets.Length; i++)
                Count(targets[i], ref bound, ref total);
        }

        /// <summary>确保节点带 Image（美术没给图时补上组件，免得运行时拿不到）</summary>
        private static Transform EnsureImage(Transform t)
        {
            if (t == null) return null;
            if (t.GetComponent<Image>() == null && t.GetComponent<RawImage>() == null)
                t.gameObject.AddComponent<Image>();
            return t;
        }

        /// <summary>合数数字的字号（1920×1080 设计像素）</summary>
        private const int BlowCounterFontSize = 72;

    }
}
