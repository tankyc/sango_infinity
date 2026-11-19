using Sango.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>
    /// 官职列表的一行。两处界面共用同一个类，靠列数区分：
    /// <list type="bullet">
    /// <item>7 列 —— 主界面的官职表（window_official_set_org/root/official_list）：
    ///   官职名 / 功绩 / 指挥 / 俸禄 / 效果 / 武将 / 官品（列名见预制体 textField_11~textField_17）。</item>
    /// <item>6 列 —— "可任命官职"面板（ofnode）：
    ///   官职名 / 官品 / 功绩 / 指挥 / 俸禄 / 效果（列名见 textField_17~textField_22）。</item>
    /// </list>
    ///
    /// 【为什么要重排文本槽】预制体里 <c>uITextItems</c> 的绑定顺序是按节点名（textItem_1..7），
    /// 而实际显示顺序由 content 的 HorizontalLayoutGroup 按 siblingIndex 决定
    /// （官职表里 textItem_7 排在第一位）。两者不一致会让"官职名"显示到错误的列里，
    /// 因此 Awake 时按 siblingIndex 重排一次（见 <see cref="RebuildTextItems"/>）。
    /// </summary>
    public class UIOfficialItem : MonoBehaviour
    {
        public int index;

        public delegate void OnSelect(UIOfficialItem item);
        public OnSelect onSelected;
        public OnSelect onDoubleSelected;

        public RectTransform contentRect;
        public Image selectImg;

        /// <summary>不可用遮罩（预制体节点 disable）：该官职对当前武将不满足任职条件时显示</summary>
        public Image disableImg;

        /// <summary>任免状态图标（预制体节点 up）：显示"可晋升 / 晋升 / 降格"三种图之一</summary>
        public Image stateImg;

        public UITextItem[] uITextItems;

        /// <summary>
        /// 官职行相对「参照武将」的任免状态。
        /// 三种图的语义取自界面右上角图例 window_official_set_org/root/ferld（可晋升 / 降格 / 晋升）。
        /// 【原版无对应实现】原版把这段 UI 逻辑放在已删除的界面代码里（s11_removed），
        /// 只有 <c>RankDialog::slot_.enabled</c>（能否任命）保留在 s11_sys_ui_rank_dialog.cpp:118-132，
        /// 因此"晋升 / 降格 / 可晋升"的三态划分是本项目按图例语义自行定义。
        /// </summary>
        public enum OfficialStateType
        {
            /// <summary>不显示（没有参照武将，或该武将不满足本官职的任职条件）</summary>
            None = 0,

            /// <summary>可晋升：满足任职条件，且该官职不低于其现任（含原本没有官职）</summary>
            CanPromote = 1,

            /// <summary>晋升：满足任职条件，且该官职品级高于其现任官职</summary>
            Promote = 2,

            /// <summary>降格：满足任职条件，但该官职品级低于其现任官职</summary>
            Demote = 3,
        }

        /// <summary>三态图标的图片（由界面从图例节点注入，避免在这里硬编码图集文件名）</summary>
        UnityEngine.Sprite stateCanPromoteSprite;
        UnityEngine.Sprite statePromoteSprite;
        UnityEngine.Sprite stateDemoteSprite;

        /// <summary>stateImg 在预制体里自带的图（"可晋升"）；注入失败时用它兜底</summary>
        UnityEngine.Sprite defaultStateSprite;

        /// <summary>晋升色（与原版图例"晋升"的绿色图标同色系）</summary>
        static readonly Color PromoteColor = new Color(0.45f, 1f, 0.45f);

        /// <summary>降格色（与原版图例"降格"的红色图标同色系）</summary>
        static readonly Color DemoteColor = new Color(1f, 0.45f, 0.45f);

        readonly float doubleClickTimer = 0.3f;
        float lastPressTime;

        /// <summary>本行当前承载的官职；null 表示空行</summary>
        public Official official;

        /// <summary>本行当前承载的任职者；null 表示该官职空缺</summary>
        public Person person;

        /// <summary>是否为"选择官职"面板里的行（6 列布局）</summary>
        public bool isSelectPanel;

        void Awake()
        {
            InitColumns();

            // 预制体里 disable 遮罩与 up 状态图标都是常显（enabled = true）的，
            // 运行时默认全部关掉，等宿主按实际数据刷新，否则一打开界面满屏灰框和箭头。
            if (disableImg != null)
                disableImg.enabled = false;
            if (stateImg != null)
            {
                defaultStateSprite = stateImg.sprite;
                stateImg.enabled = false;
            }
        }

        /// <summary>
        /// 注入三种状态图标。
        /// 图例节点的图会随美术调整，所以不在代码里写死 sprite 名，由界面在打开时传进来。
        /// </summary>
        public void SetStateSprites(UnityEngine.Sprite canPromote, UnityEngine.Sprite promote, UnityEngine.Sprite demote)
        {
            stateCanPromoteSprite = canPromote;
            statePromoteSprite = promote;
            stateDemoteSprite = demote;
        }

        /// <summary>
        /// 按 content 下子节点的实际排列顺序（= 显示顺序）重排文本槽。
        /// 只能按 siblingIndex 排：预制体里 textItem_1 并不一定是第一列
        /// （官职表的第一个文本节点是 textItem_7）。
        ///
        /// 【为什么外部也要调一次】本 prefab 的行是**嵌套 prefab 实例**，Awake 阶段
        /// 子节点的 siblingIndex 还没展开完（此时读到的顺序与最终显示顺序不一致），
        /// 所以宿主界面在 OnOpen 时必须再调用一次，见 <see cref="UIOfficialSet.OnOpen"/>。
        /// </summary>
        public void InitColumns()
        {
            if (uITextItems == null || uITextItems.Length == 0)
                return;

            for (int i = 0; i < uITextItems.Length; i++)
            {
                if (uITextItems[i] == null)
                    return;   // 有空洞就不动，保持预制体配置
            }

            //System.Array.Sort(uITextItems, (a, b) =>
            //    a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
        }

        void ScrollCellIndex(int idx)
        {
            index = idx;
        }

        public void OnClick()
        {
        }

        public void Clear()
        {
            ClearText();
            official = null;
            person = null;
        }

        void ClearText()
        {
            if (uITextItems == null) return;
            for (int i = 0; i < uITextItems.Length; i++)
            {
                if (uITextItems[i] != null)
                    uITextItems[i].SetText("");
            }
        }

        void SetText(int column, string content)
        {
            if (uITextItems == null || column < 0 || column >= uITextItems.Length)
                return;
            UITextItem item = uITextItems[column];
            if (item != null)
                item.SetText(content);
        }

        /// <summary>
        /// 填充一行官职。
        /// 只负责"官职数据 → 各列文本"，本行相对参照武将的晋升/降格表现交给 <see cref="SetCompare"/>。
        /// </summary>
        /// <param name="holder">该官职的现任者；null 表示空缺</param>
        /// <param name="target">官职</param>
        public void SetOfficial(Person holder, Official target)
        {
            official = target;
            person = holder;

            if (target == null)
            {
                ClearText();
                ResetCompareStyle();
                return;
            }

            // 列表里只放 5 字以内的短描述（"政+4"），完整说明由界面在选中该行时显示到说明栏，
            // 否则一列 400 宽的单元格会被整段文字挤爆（旧实现就是直接塞 effect_desc）。
            string effectText = target.EffectShort;
            string holderText = holder != null ? holder.ColorName : "";

            if (isSelectPanel || (uITextItems != null && uITextItems.Length <= 6))
            {
                // 6 列：官职名 / 功绩 / 指挥 / 俸禄 / 效果（短描述）/ 现任武将
                SetText(0, target.Name);
                SetText(1, target.RequiredMerit.ToString());
                SetText(2, target.troopsLimit.ToString());
                SetText(3, target.cost.ToString());
                SetText(4, effectText);
                SetText(5, holderText);
            }
            else
            {
                // 7 列：官职名 / 功绩 / 指挥 / 俸禄 / 效果（短描述）/ 现任武将
                SetText(0, target.Name);
                SetText(1, target.RequiredMerit.ToString());
                SetText(2, target.troopsLimit.ToString());
                SetText(3, target.cost.ToString());
                SetText(4, effectText);
                SetText(5, holderText);
            }

            // 本行是 Loop 列表的复用行：先把上一次的"晋升/降格"染色与状态图标复位，
            // 再由宿主调用 SetCompare 按当前参照武将重新表现。
            ResetCompareStyle();
        }

        /// <summary>复位"晋升/降格"表现：官名回白色、状态图标收起</summary>
        void ResetCompareStyle()
        {
            if (uITextItems != null && uITextItems.Length > 0 && uITextItems[0] != null)
                uITextItems[0].SetColor(Color.white);
            SetState(OfficialStateType.None);
        }

        /// <summary>
        /// 相对「参照武将」刷新本行的任免表现：官名染色 + 状态图标。
        /// 配色与原版图例一致（window_official_set_org/root/ferld）：晋升=绿、降格=红、可晋升/同级=白。
        /// </summary>
        /// <param name="refPerson">参照武将：正在拖动的武将 &gt; 右表选中的武将；null 表示无参照</param>
        /// <param name="canAssign">该武将是否满足本官职的任职条件（由 CityUpgradeOfficial.CanAssign 判定）</param>
        public void SetCompare(Person refPerson, bool canAssign)
        {
            Color color = Color.white;
            OfficialStateType state = OfficialStateType.None;

            if (refPerson != null && official != null && canAssign)
            {
                Official refOfficial = refPerson.Official;
                if (refOfficial == null || refOfficial.Id == 0)
                {
                    // 原本没有官职：能任就是"可晋升"，不存在降格
                    state = OfficialStateType.CanPromote;
                }
                else if (official.level < refOfficial.level)
                {
                    state = OfficialStateType.Promote;
                    color = PromoteColor;
                }
                else if (official.level > refOfficial.level)
                {
                    state = OfficialStateType.Demote;
                    color = DemoteColor;
                }
                else
                {
                    // 同级平调：按"可晋升"显示，不染绿也不染红
                    state = OfficialStateType.CanPromote;
                }
            }

            if (uITextItems != null && uITextItems.Length > 0 && uITextItems[0] != null)
                uITextItems[0].SetColor(color);

            SetState(state);
        }

        /// <summary>
        /// 设置状态图标。None 或对应图缺失时收起 —— 宁可没有图标，也不要显示错语义的图。
        /// </summary>
        public void SetState(OfficialStateType state)
        {
            if (stateImg == null)
                return;

            UnityEngine.Sprite sprite = null;
            switch (state)
            {
                case OfficialStateType.CanPromote:
                    // 注入的图例 → Awake 备份的图 → 预制体当前图（up 节点默认就是"可晋升"）。
                    // 三级兜底是为了"图例节点被删 / 注入没跑到"时，唯一一个必显的状态仍然能显示出来。
                    sprite = stateCanPromoteSprite;
                    if (sprite == null)
                        sprite = defaultStateSprite;
                    if (sprite == null)
                        sprite = stateImg.sprite;
                    break;
                case OfficialStateType.Promote:
                    sprite = statePromoteSprite;
                    break;
                case OfficialStateType.Demote:
                    sprite = stateDemoteSprite;
                    break;
            }

            if (sprite == null)
            {
                stateImg.enabled = false;
                return;
            }

            stateImg.sprite = sprite;
            stateImg.enabled = true;
        }

        /// <summary>官品：1 品最高（对应官职表的 level 0），10 品最低（level 9）</summary>
        static string GetRankText(Official target)
        {
            if (target == null) return "";
            return (target.level + 1).ToString();
        }

        public void SetSelected(bool b)
        {
            if (selectImg != null)
                selectImg.enabled = b;
        }

        public bool IsSelected()
        {
            return selectImg != null && selectImg.enabled;
        }

        public void SetOver(bool b)
        {
           
        }

        public void SetPressd(bool b)
        {
            if (!b)
            {
                float off = Time.realtimeSinceStartup - lastPressTime;
                if (off <= doubleClickTimer)
                {
                    onDoubleSelected?.Invoke(this);
                }
                else
                {
                    onSelected?.Invoke(this);
                }
                lastPressTime = Time.realtimeSinceStartup;
            }
        }

        /// <summary>
        /// 不可用显示：该官职对"参照武将"不满足任职条件时，亮出预制体里的 disable 遮罩（灰色九宫格边框）。
        /// 只动遮罩、不动文字颜色 —— 官职名的绿（晋升）/ 红（降格）由 <see cref="SetCompare"/> 控制，
        /// 两者叠加才能同时表达"能不能任"和"任了是升还是降"。
        /// 预制体没绑遮罩时退回整行文字变灰（兼容老预制体）。
        /// </summary>
        public void SetDisable(bool b)
        {
            if (disableImg != null)
            {
                disableImg.enabled = b;
                return;
            }

            Color color = b ? new Color(0.42f, 0.42f, 0.42f, 1f) : Color.white;
            if (uITextItems == null)
                return;
            for (int i = 0; i < uITextItems.Length; i++)
            {
                if (uITextItems[i] != null)
                    uITextItems[i].SetColor(color);
            }
        }
    }
}
