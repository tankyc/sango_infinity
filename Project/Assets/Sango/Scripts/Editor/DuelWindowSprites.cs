/*
 * 文件名：DuelWindowSprites.cs
 * 描述：单挑界面用到的精灵图（取自 Mods 图集）的集中登记表
 *
 * 图集说明（Assets/Mods/Content/Assets/UI/AtlasTexture/）：
 *   4846-4  窗口边框 / 武将信息条底板
 *   4849-2  单挑 HUD：水墨体力条 / 斗志条 / 条槽 / 墨迹装饰 / 暗色墨板
 *   4849-6  单挑命令按钮的墨板（红 / 深红 / 蓝 / 紫 / 灰黑 多种配色）
 *
 * 约定：这里只登记"已确认存在且语义明确"的图片；
 *       未确认的节点保持原样（找不到就不处理）。
 */

using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.EditorTools
{
    /// <summary>单挑界面精灵图登记与套用</summary>
    public static class DuelWindowSprites
    {
        private const string Atlas = "Assets/Mods/Content/Assets/UI/AtlasTexture/";

        #region 精灵图清单

        // ---- 顶部武将条 HUD（4849-1）----
        // 该图集是原版单挑界面的 HUD 素材：水墨长条、墨板、必杀字样、合数数字。

        /// <summary>头像底框（带黑边的方板）</summary>
        public const string PortraitPlate = Atlas + "4849-1/4849-1_17.png";
        /// <summary>姓名条底（横向水墨笔刷）</summary>
        public const string NameStroke = Atlas + "4849-1/4849-1_11.png";
        /// <summary>体力 / 斗志的墨迹长条（左端为水墨飞白，右段是凹槽）</summary>
        public const string BarInk = Atlas + "4849-1/4849-1_14.png";
        /// <summary>合数单位「合」</summary>
        public const string UnitHe = Atlas + "4849-1/4849-1_15.png";
        /// <summary>必杀字样 · 米白勾边（可用状态）</summary>
        public const string SpecialIvory = Atlas + "4849-1/4849-1_30.png";
        /// <summary>必杀字样 · 金色（已选 / 强调）</summary>
        public const string SpecialGold = Atlas + "4849-1/4849-1_29.png";
        /// <summary>必杀字样 · 灰色（不可用）</summary>
        public const string SpecialGray = Atlas + "4849-1/4849-1_28.png";
        /// <summary>墨迹团（头像背后的水墨衬底 / 装饰）</summary>
        public const string InkBlob = Atlas + "4849-1/4849-1_13.png";

        // ---- 单挑 HUD（4849-2）----

        /// <summary>体力条填充 · 黄绿（左方阵营）</summary>
        public const string HpFillYellowGreen = Atlas + "4849-2/4849-2_4.png";
        /// <summary>体力条填充 · 亮绿（左方阵营变体）</summary>
        public const string HpFillGreen = Atlas + "4849-2/4849-2_5.png";
        /// <summary>体力条填充 · 青蓝（右方阵营）</summary>
        public const string HpFillBlue = Atlas + "4849-2/4849-2_7.png";
        /// <summary>斗志条填充 · 青</summary>
        public const string SpiritFillLeft = Atlas + "4849-2/4849-2_6.png";
        /// <summary>斗志条填充 · 青（变体）</summary>
        public const string SpiritFillRight = Atlas + "4849-2/4849-2_8.png";
        /// <summary>状态条底槽</summary>
        public const string BarSlot = Atlas + "4849-2/4849-2_10.png";
        /// <summary>状态条底槽 · 残缺变体（右方阵营）</summary>
        public const string BarSlotAlt = Atlas + "4849-2/4849-2_12.png";
        /// <summary>横向渐隐墨迹（合数背后的装饰）</summary>
        public const string InkStrokeWide = Atlas + "4849-2/4849-2_2.png";
        /// <summary>斜向墨迹</summary>
        public const string InkStrokeSlant = Atlas + "4849-2/4849-2_11.png";
        /// <summary>暗色墨板（卡牌底 / 日志底）</summary>
        public const string InkPlateDark = Atlas + "4849-2/4849-2_13.png";
        /// <summary>暗色墨板 · 变体</summary>
        public const string InkPlateDarkAlt = Atlas + "4849-2/4849-2_14.png";
        /// <summary>带翼大墨板（停止按钮）</summary>
        public const string InkPlateWing = Atlas + "4849-2/4849-2_0.png";

        // ---- 命令按钮墨板（4849-6）----
        // 同一块斜切墨板的不同配色/状态

        /// <summary>墨板 · 红（亮）—— 必杀等强调按钮</summary>
        public const string PlateRed = Atlas + "4849-6/4849-6_0.png";
        /// <summary>墨板 · 红（暗）</summary>
        public const string PlateRedDark = Atlas + "4849-6/4849-6_1.png";
        /// <summary>墨板 · 深红</summary>
        public const string PlateDeepRed = Atlas + "4849-6/4849-6_2.png";
        /// <summary>墨板 · 蓝（亮）—— 选中的行动方针</summary>
        public const string PlateBlue = Atlas + "4849-6/4849-6_3.png";
        /// <summary>墨板 · 蓝</summary>
        public const string PlateBlueDim = Atlas + "4849-6/4849-6_4.png";
        /// <summary>墨板 · 灰黑 —— 普通行动方针</summary>
        public const string PlateGray = Atlas + "4849-6/4849-6_5.png";
        /// <summary>墨板 · 紫（亮）</summary>
        public const string PlatePurple = Atlas + "4849-6/4849-6_6.png";
        /// <summary>墨板 · 紫（暗）</summary>
        public const string PlatePurpleDark = Atlas + "4849-6/4849-6_7.png";
        /// <summary>墨板 · 深灰</summary>
        public const string PlateGrayDark = Atlas + "4849-6/4849-6_8.png";

        #endregion

        /// <summary>把所有登记好的图片套用到单挑界面</summary>
        public static string Apply(GameObject root)
        {
            if (root == null) return "root is null";

            int applied = 0;
            System.Text.StringBuilder missing = new System.Text.StringBuilder();

            // ---- 左方阵营：头像底框 + 姓名墨刷 + 墨迹长条 + 黄绿体力 / 青斗志 ----
            applied += ApplySide(root, "LeftPanel", false, HpFillYellowGreen, SpiritFillLeft, missing);

            // ---- 右方阵营：同一套墨迹长条，填充换成青蓝色且自右向左涨 ----
            applied += ApplySide(root, "RightPanel", true, HpFillBlue, SpiritFillRight, missing);

            // ---- 合数：「N」+ 图集中的「合」字 ----
            applied += SetSprite(root, "BlowCounter_bg/BlowCounterUnit", UnitHe, missing);

            // ---- 四条行动方针：灰黑墨板；选中态换蓝色墨板 ----
            for (int i = 0; i < 4; i++)
            {
                applied += SetSprite(root, "CommandPanel/Stance" + i, PlateGray, missing);
                applied += SetSpriteState(root, "CommandPanel/Stance" + i, PlateBlue, PlateBlueDim, missing);
            }

            // ---- 秘杀：图集里自带「必殺」字样，套上后隐藏原来的文字标签 ----
            applied += SetSprite(root, "CommandPanel/BtnSpecial", SpecialIvory, missing);
            applied += SetSpriteState(root, "CommandPanel/BtnSpecial", SpecialGold, SpecialGray, missing);
            applied += HideLabel(root, "CommandPanel/BtnSpecial");
            applied += SetWhiteTint(root, "CommandPanel/BtnSpecial");

            // ---- 取消：红色系墨板 ----
            applied += SetSprite(root, "CommandPanel/BtnSpecialCancel", PlateDeepRed, missing);

            // ---- 停止：带翼大墨板 ----
            applied += SetSprite(root, "CommandPanel/BtnPlay", InkPlateWing, missing);
            applied += SetSpriteState(root, "CommandPanel/BtnPlay", InkPlateWing, InkStrokeSlant, missing);

            // ---- 交替：蓝色墨板 ----
            for (int i = 0; i < 3; i++)
            {
                applied += SetSprite(root, "CommandPanel/Switch" + i, PlateBlue, missing);
            }

            // ---- 战报：暗色墨板 ----
            applied += SetSprite(root, "LogBg", InkPlateDarkAlt, missing);

            // ---- 卡牌：只用外框墨板；卡面保持无图（由收尾步骤填半透明底色），
            //      避免拿墨迹图拉成"白色实心块" ----
            applied += SetSprite(root, "CardArea/CardLeft", InkPlateDark, missing);
            applied += SetSprite(root, "CardArea/CardRight", InkPlateDark, missing);

            string msg = "应用精灵图 " + applied + " 处";
            if (missing.Length > 0) msg += "；缺失：" + missing;
            return msg;
        }

        /// <summary>
        /// 套用一侧（左 / 右）阵营的全部 HUD 美术：
        /// 头像底框、姓名墨刷、体力 / 斗志的墨迹长条及其填充。
        /// 左侧阵营的填充节点名为 fill，右侧为 fill_r（运行时据此决定填充方向）。
        /// </summary>
        private static int ApplySide(GameObject root, string panel, bool mirrored,
            string hpFill, string spiritFill, System.Text.StringBuilder missing)
        {
            int applied = 0;
            string fillName = mirrored ? "fill_r" : "fill";

            // 主将
            applied += SetSprite(root, panel + "/person/headbg", PortraitPlate, missing);
            applied += SetSprite(root, panel + "/person/NamePlate", NameStroke, missing);
            applied += SetSprite(root, panel + "/person/hpInk", BarInk, missing);
            applied += SetSprite(root, panel + "/person/hp/" + fillName, hpFill, missing);
            // 斗志条是一根细槽，不用水墨长条（否则条下方会出现一大团墨迹）
            applied += SetSprite(root, panel + "/person/mpInk", BarSlot, missing);
            applied += SetSprite(root, panel + "/person/mp/" + fillName, spiritFill, missing);

            // 副将（只显示体力）
            for (int i = 1; i <= 2; i++)
            {
                string sub = panel + "/person_" + i;
                applied += SetSprite(root, sub + "/headbg", PortraitPlate, missing);
                applied += SetSprite(root, sub + "/NamePlate", NameStroke, missing);
                applied += SetSprite(root, sub + "/hpInk", BarInk, missing);
                applied += SetSprite(root, sub + "/hp/" + fillName, hpFill, missing);
            }

            return applied;
        }

        /// <summary>
        /// 把节点的 Image 颜色重置为白色。
        /// 图集里的素材自带配色，若沿用旧稿留下的着色会被染成别的颜色。
        /// </summary>
        private static int SetWhiteTint(GameObject root, string path)
        {
            Transform t = FindPath(root.transform, path);
            if (t == null) return 0;

            Image img = t.GetComponent<Image>();
            if (img == null) return 0;
            img.color = Color.white;

            // 按钮的普通态颜色同样会乘到图片上，一并归一
            Button btn = t.GetComponent<Button>();
            if (btn != null)
            {
                ColorBlock colors = btn.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = Color.white;
                colors.pressedColor = Color.white;
                colors.selectedColor = Color.white;
                colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                btn.colors = colors;
            }
            return 1;
        }

        /// <summary>隐藏按钮上的文字标签（改用图集里自带字样的图片时使用）</summary>
        private static int HideLabel(GameObject root, string path)
        {
            Transform t = FindPath(root.transform, path);
            if (t == null) return 0;

            Transform label = null;
            for (int i = 0; i < t.childCount; i++)
            {
                if (t.GetChild(i).name == "Label") { label = t.GetChild(i); break; }
            }
            if (label == null) return 0;

            label.gameObject.SetActive(false);
            return 1;
        }

        /// <summary>按路径设置 Image 的 sprite（保留原有 Image 类型与颜色）</summary>
        private static int SetSprite(GameObject root, string path, string spritePath, System.Text.StringBuilder missing)
        {
            Transform t = FindPath(root.transform, path);
            if (t == null) return 0;

            Image img = t.GetComponent<Image>();
            if (img == null) return 0;

            UnityEngine.Sprite sp = AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(spritePath);
            if (sp == null)
            {
                // 找不到精灵图则保持原样，不做任何处理
                if (missing != null) missing.Append(spritePath).Append(" ");
                return 0;
            }

            img.sprite = sp;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            return 1;
        }

        /// <summary>设置按钮的悬停 / 按下外观</summary>
        private static int SetSpriteState(GameObject root, string path, string highlight, string pressed, System.Text.StringBuilder missing)
        {
            Transform t = FindPath(root.transform, path);
            if (t == null) return 0;

            Button btn = t.GetComponent<Button>();
            if (btn == null) return 0;

            UnityEngine.Sprite hi = AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(highlight);
            UnityEngine.Sprite pr = AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(pressed);
            if (hi == null || pr == null)
            {
                if (missing != null) missing.Append(highlight).Append(" ");
                return 0;
            }

            SpriteState state = btn.spriteState;
            state.highlightedSprite = hi;
            state.pressedSprite = pr;
            state.selectedSprite = hi;
            btn.spriteState = state;
            return 1;
        }

        /// <summary>按相对路径查找节点</summary>
        private static Transform FindPath(Transform root, string path)
        {
            Transform cur = root;
            string[] parts = path.Split('/');
            for (int i = 0; i < parts.Length; i++)
            {
                if (cur == null) return null;
                Transform next = null;
                for (int c = 0; c < cur.childCount; c++)
                {
                    if (cur.GetChild(c).name == parts[i]) { next = cur.GetChild(c); break; }
                }
                cur = next;
            }
            return cur;
        }
    }
}
