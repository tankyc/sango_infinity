using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    /// <summary>
    /// 剧本选择界面
    /// </summary>
    public class UITechniqueItem : MonoBehaviour
    {
        public RectTransform root;

        public Text techName;
        //public Text costGold;
        //public Text costTP;
        //public Text costCounter;
        public Image progress;
        public Image doneImg;
        public Image selectImg;
        public Image bg;
        public Text needAttributeType;
        public RectTransform itemNode;
        public Image invalidMask;

        public Technique technique;

        public System.Action<UITechniqueItem> onClick;

        public UITechniqueItem SetTechnique(Technique t)
        {
            technique = t;

            // 【空科技兜底】
            // 这一路同时被「研究完成」演出（UITechniqueComplete）和科技树界面
            // （UITechniqueArea）复用，而 MOD 改动科技表后，旧存档里引用的科技 Id
            // 可能已经不存在 —— 上游 Force.AddTechnique 对查不到的 Id 返回 null。
            // 直接解引用 t.Name 会抛 NullReferenceException（实测崩溃栈：
            // UITechniqueComplete.OnOpen → 本方法）。这里把显示内容清空，
            // 让条目退化成空占位，调用方仍可安全继续遍历，不会打断整个界面。
            if (t == null)
            {
                if (techName != null) techName.text = string.Empty;
                if (needAttributeType != null) needAttributeType.text = string.Empty;
                return this;
            }

            techName.text = t.Name;
            //costGold.text = t.goldCost.ToString();
            //costTP.text = t.techPointCost.ToString();
            //costCounter.text = t.counter.ToString();
            needAttributeType.text = Scenario.Cur.Variables.GetAttributeNameWithColor(t.needAttr);
            bg.color = t.tabColor;
            doneImg.color = t.tabColor;
            return this;
        }

        public void OnClick()
        {
            onClick?.Invoke(this);
        }

        public UITechniqueItem SetAction(System.Action<UITechniqueItem> action)
        {
            onClick = action;
            return this;
        }

        public UITechniqueItem SetValid(bool b)
        {
            doneImg.gameObject.SetActive(b);
            return this;
        }
        public bool IsValid() { return doneImg.enabled; }

        public UITechniqueItem SetCanResearch(bool b)
        {
            invalidMask.enabled = !b;
            return this;
        }

        public bool CanResearch() { return !invalidMask.enabled; }

        public UITechniqueItem SetProgress(float p)
        {
            return this;
        }

        public UITechniqueItem SetSelected(bool b)
        {
            selectImg.enabled = b;
            return this;
        }

    }
}
