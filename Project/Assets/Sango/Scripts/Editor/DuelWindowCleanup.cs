/*
 * 文件名：DuelWindowCleanup.cs
 * 描述：单挑界面 window_duel.prefab 的清理与收尾
 *
 * 背景：
 *   该 prefab 经历过多次迭代，早期版本的节点（占位进度条 bars、旧合数文本、旧提示文本、
 *   旧日志内容、"出现武将"标题等）仍残留在层级里，与新结构重叠渲染。
 *   本类负责把这些历史残留清理干净，并把文案/初始数值对齐《三国志11》单挑界面。
 *
 * 原则：
 *   1. 只删除"确认无用"的残留节点，不碰美术与已有正确元素。
 *   2. 文案与初始数值集中在这里，便于后续统一调整。
 */

using UnityEngine;
using UnityEngine.UI;

namespace Sango.EditorTools
{
    /// <summary>单挑界面清理与收尾</summary>
    public static class DuelWindowCleanup
    {
        /// <summary>体力 / 斗志的显示上限（与 Duel.MaxHP / Duel.MaxSpirit 一致）</summary>
        private const int MaxHp = 100;
        private const int MaxSpirit = 300;

        /// <summary>执行清理</summary>
        public static string Run(GameObject root)
        {
            if (root == null) return "root is null";

            int removed = 0;
            removed += RemoveLegacyNodes(root);
            removed += NormalizeBars(root);
            NormalizeTexts(root);

            return "清理残留节点 " + removed + " 个";
        }

        /// <summary>
        /// 收尾。现在只剩一件事：卡牌头像是空的 RawImage，编辑期会渲染成白色实心方块，
        /// 先置成全透明，运行时由 CardDuelView 填入头像后恢复不透明。
        ///
        /// 以前这里还会把武将条底板调透明、隐藏旧窗口框、把合数提到最上层、改卡面色，
        /// 这些都会动到位置 / 层级 / 美术图，覆盖美术手工调好的稿子，已全部去掉——
        /// 相关节点现在由美术自己掌握。
        /// </summary>
        public static string Polish(GameObject root)
        {
            if (root == null) return "root is null";

            HideEmptyRawImage(root, "CardArea/CardLeft/face/head");
            HideEmptyRawImage(root, "CardArea/CardRight/face/head");

            return "收尾：空头像占位置透明";
        }

        #region 删除历史残留

        /// <summary>删除早期版本遗留、且与现结构冲突的节点</summary>
        private static int RemoveLegacyNodes(GameObject root)
        {
            int count = 0;

            // 1) 早期脚本在武将位上建过一套占位进度条 bars，现由 hp / mp 承担
            count += DestroyAll(root.transform, "bars");

            // 2) 根节点下残留的旧合数文本（新合数挂在 BlowCounter_bg 之下）
            count += DestroyAllAtDepth(root.transform, "BlowCounter", 1);

            // 3) 根节点下残留的旧操作提示（新提示挂在 CommandPanel 之下）
            count += DestroyAllAtDepth(root.transform, "SkipHint", 1);

            // 4) 旧日志内容容器（新日志文本直接命名为 log）
            count += DestroyChild(FindPath(root.transform, "LogBg"), "content");

            // 5) 旧窗口标题「出现武将」及其底框，原版单挑界面没有该标题
            Transform frame = FindPath(root.transform, "win_frame3");
            if (frame != null)
            {
                count += DestroyChild(frame, "title");
                count += DestroyChild(frame, "titleframe");
            }

            return count;
        }

        /// <summary>
        /// 安全移除节点。
        /// 属于嵌套 prefab 实例的节点不允许直接删除（Unity 限制），
        /// 此时改为改名并隐藏，既不参与渲染也不会干扰查找。
        /// </summary>
        private static int SafeRemove(GameObject go)
        {
            if (go == null) return 0;
            try
            {
                if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(go))
                {
                    go.name = "_legacy_" + go.name;
                    go.SetActive(false);
                    return 1;
                }
                UnityEngine.Object.DestroyImmediate(go);
                return 1;
            }
            catch (System.Exception)
            {
                go.name = "_legacy_" + go.name;
                go.SetActive(false);
                return 1;
            }
        }

        /// <summary>递归删除所有同名节点</summary>
        private static int DestroyAll(Transform parent, string name)
        {
            if (parent == null) return 0;
            int count = 0;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                count += DestroyAll(child, name);
                if (child.name == name)
                {
                    SafeRemove(child.gameObject);
                    count++;
                }
            }
            return count;
        }

        /// <summary>只删除指定深度上的同名节点（1 = 直接子节点）</summary>
        private static int DestroyAllAtDepth(Transform parent, string name, int depth)
        {
            if (parent == null) return 0;
            if (depth <= 1)
            {
                int count = 0;
                for (int i = parent.childCount - 1; i >= 0; i--)
                {
                    if (parent.GetChild(i).name == name)
                    {
                        SafeRemove(parent.GetChild(i).gameObject);
                        count++;
                    }
                }
                return count;
            }

            int total = 0;
            for (int i = 0; i < parent.childCount; i++)
                total += DestroyAllAtDepth(parent.GetChild(i), name, depth - 1);
            return total;
        }

        /// <summary>删除指定名字的直接子节点</summary>
        private static int DestroyChild(Transform parent, string name)
        {
            if (parent == null) return 0;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                if (parent.GetChild(i).name == name)
                {
                    SafeRemove(parent.GetChild(i).gameObject);
                    return 1;
                }
            }
            return 0;
        }

        #endregion

        #region 状态条归位

        /// <summary>
        /// 统一三个武将位的状态条结构：
        ///   主将保留 hp + mp；副将只保留 hp（与《三国志11》一致，副将不显示斗志）。
        /// </summary>
        private static int NormalizeBars(GameObject root)
        {
            int count = 0;
            count += NormalizeSlot(FindPath(root.transform, "LeftPanel/person"), true, false);
            count += NormalizeSlot(FindPath(root.transform, "LeftPanel/person_1"), false, false);
            count += NormalizeSlot(FindPath(root.transform, "LeftPanel/person_2"), false, false);
            count += NormalizeSlot(FindPath(root.transform, "RightPanel/person"), true, true);
            count += NormalizeSlot(FindPath(root.transform, "RightPanel/person_1"), false, true);
            count += NormalizeSlot(FindPath(root.transform, "RightPanel/person_2"), false, true);
            return count;
        }

        /// <summary>
        /// 卡牌头像：RawImage 在没有贴图时会渲染成白色实心方块。
        /// 编辑期先置为全透明，运行时由 CardDuelView 填入头像后恢复不透明。
        /// </summary>
        private static void HideEmptyRawImage(GameObject root, string path)
        {
            Transform t = FindPath(root.transform, path);
            if (t == null) return;
            RawImage raw = t.GetComponent<RawImage>();
            if (raw == null) return;
            raw.color = new Color(1f, 1f, 1f, 0f);
        }

        /// <summary>
        /// 整理一个武将位上的体力 / 斗志条。
        /// 位置与尺寸由 DuelWindowBuilder 负责（要贴合墨迹长条的凹槽），
        /// 这里只落实《三国志11》的规则：副将不显示斗志条。
        /// </summary>
        private static int NormalizeSlot(Transform slot, bool withSpirit, bool mirrored)
        {
            if (slot == null) return 0;
            if (withSpirit) return 0;

            int count = 0;
            foreach (string name in new[] { "mp", "mpInk" })
            {
                Transform t = FindChild(slot, name);
                if (t == null || !t.gameObject.activeSelf) continue;
                t.gameObject.SetActive(false);
                count++;
            }
            return count;
        }

        #endregion

        #region 文案与初始数值

        private static void NormalizeTexts(GameObject root)
        {
            // 合数：只写数字，单位「合」由界面上的图片承担
            SetText(root, "BlowCounter_bg/BlowCounter", "0");

            // 停止按钮（原版为「停止」，本项目在暂停时复用为「继续」）
            SetText(root, "CommandPanel/BtnPlay/Label", "停止");
            SetText(root, "CommandPanel/SkipHint", "可对武将下达指示");

            // 体力 / 斗志的初始数值，避免沿用旧稿的 333/1000
            string[] slots =
            {
                "LeftPanel/person", "LeftPanel/person_1", "LeftPanel/person_2",
                "RightPanel/person", "RightPanel/person_1", "RightPanel/person_2",
            };
            foreach (string slot in slots)
            {
                SetText(root, slot + "/hp/txt", MaxHp + "/" + MaxHp);
                Transform mp = FindPath(root.transform, slot + "/mp/txt");
                if (mp != null) SetText(root, slot + "/mp/txt", MaxSpirit + "/" + MaxSpirit);
            }

            // 武将姓名 / 武力留空，由运行时填充
            foreach (string slot in slots)
            {
                SetText(root, slot + "/name", "");
                SetText(root, slot + "/Strength", "武力 0");
            }
        }

        private static void SetText(GameObject root, string path, string value)
        {
            Transform t = FindPath(root.transform, path);
            if (t == null) return;
            Text txt = t.GetComponent<Text>();
            if (txt != null) txt.text = value;
        }

        #endregion

        #region 通用工具

        private static void SetRect(Transform t, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            RectTransform rt = t as RectTransform;
            if (rt == null) return;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static Transform FindChild(Transform parent, string name)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).name == name) return parent.GetChild(i);
            }
            return null;
        }

        private static Transform FindPath(Transform root, string path)
        {
            Transform cur = root;
            string[] parts = path.Split('/');
            for (int i = 0; i < parts.Length; i++)
            {
                cur = FindChild(cur, parts[i]);
                if (cur == null) return null;
            }
            return cur;
        }

        #endregion
    }
}
