using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core;
using Sango.Mod;

namespace Sango.UI
{
    /// <summary>
    /// MOD项UI
    /// </summary>
    public class UIModItem : MonoBehaviour
    {
        public Text modNameText;
        public Text modVersionText;
        public Toggle enableToggle;
        public Image selectedImage;
        public Transform root;
        public Image pressImg;
        public Image overImg;
        public Image progressImg;
        public Image bgImg;
        public UIButtonItem downloadBtn;
        public UIButtonItem deleteBtn;
        public Mod.Mod mod;

        public int targetIndex;
        private System.Action onClickCall;
        private System.Action<int, bool> onToggleCall;
        public System.Action<UIModItem> onDownloadCall;

        public Button upButton;
        public Button downButton;

        /// <summary>
        /// 所属的Mod管理窗口
        /// 上移/下移统一由 UIModManager 处理,本项只负责把自己交给窗口
        /// </summary>
        UIModManager manager;
        float pressTime;
        bool isPress = false;
        public void OnClickDownload()
        {
            //onDownloadCall?.Invoke(this);
            if (mod == null || mod.IsDownloading())
                return;

            // 下载前先检查依赖是否都能找到:找不到的依赖无法自动下载,需要先提示玩家
            List<string> missingDepends = ModManager.Instance.GetMissingDepends(mod);
            if (missingDepends.Count > 0)
            {
                GameDialog.Instance.Open(GameDialog.DialogStyle.Normal,
                    $"该模组依赖以下模组,但本地与市场均未找到:\n{string.Join("\n", missingDepends)}\n是否仍要下载?",
                    () => mod.Download());
                return;
            }

            // Download 内部会先把未安装的前置依赖依次下载完,再下载本模组
            mod.Download();
        }

        public void SetMod(Mod.Mod m)
        {
            mod = m;
            if (mod != null)
            {
                deleteBtn.gameObject.SetActive(false);
                enableToggle.gameObject.SetActive(mod.IsValidMod());
                downloadBtn.gameObject.SetActive(mod.CanUpgrage());
                SetName(mod.Name);
                if (mod.CanUpgrage())
                {
                    if (mod.IsValidMod())
                    {
                        string txt = $"{mod.MarketName} {mod.Version}→{mod.UrlVersion}:{GameUtility.FormatFileSizeStr(mod.Size)}";
                        SetVersion(txt);
                    }
                    else
                    {
                        string txt = $"{mod.MarketName} ↑{mod.UrlVersion}:{GameUtility.FormatFileSizeStr(mod.Size)}";
                        SetVersion(txt);
                    }
                }
                else
                {
                    if (mod.IsValidMod())
                        SetVersion($"{mod.MarketName} {mod.Version}");
                    else
                        SetVersion($"{mod.MarketName} {mod.UrlVersion}");
                }
            }
            else
            {
                SetSelected(false);
                SetEnabled(false);
                SetName("");
                SetVersion("");
                enableToggle.gameObject.SetActive(false);
                downloadBtn.gameObject.SetActive(false);
                deleteBtn.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 绑定所属的Mod管理窗口
        /// </summary>
        public UIModItem SetManager(UIModManager m)
        {
            manager = m;
            return this;
        }

        /// <summary>
        /// 点击上移按钮:先把自己置为当前选中项,再请求窗口上移一位
        /// </summary>
        public void OnClickUp()
        {
            if (manager == null)
                return;
            manager.OnItemSelected(this);
            manager.ModUp();
        }

        /// <summary>
        /// 点击下移按钮:先把自己置为当前选中项,再请求窗口下移一位
        /// </summary>
        public void OnClickDown()
        {
            if (manager == null)
                return;
            manager.OnItemSelected(this);
            manager.ModDown();
        }

        public UIModItem SetName(string name)
        {
            if (modNameText != null)
            {
                modNameText.text = name;
            }
            return this;
        }

        public UIModItem SetVersion(string version)
        {
            if (modVersionText != null)
            {
                modVersionText.text = version;
            }
            return this;
        }

        public void OnPressDown()
        {
            isPress = true;
            pressTime = 0;
        }

        public void OnPressUp()
        {
            isPress = false;
        }

        public UIModItem SetEnabled(bool enabled)
        {
            if (enableToggle != null)
            {
                // 用 SetIsOnWithoutNotify:列表刷新时会反复设置勾选状态,
                // 若走 isOn 会反过来触发 onValueChanged,造成"刷新即勾选/取勾"的递归
                enableToggle.SetIsOnWithoutNotify(enabled);
            }
            return this;
        }

        /// <summary>
        /// 设置选中状态
        /// 选中项会显示上移/下移按钮(用于快速调整模组顺序),非选中项隐藏按钮
        /// </summary>
        public UIModItem SetSelected(bool selected)
        {
            if (selectedImage != null)
            {
                // 只切换 sel 图片组件的 enabled,不整体隐藏 GameObject,
                // 避免影响 sel 节点下其它子对象/布局
                selectedImage.enabled = selected;
            }
            if (upButton != null)
            {
                upButton.gameObject.SetActive(selected);
            }
            if (downButton != null)
            {
                downButton.gameObject.SetActive(selected);
            }
            return this;
        }

        /// <summary>
        /// 设置上移/下移按钮是否可点击
        /// 位于列表第一位时上移不可用,位于最后一位时下移不可用
        /// </summary>
        public UIModItem SetArrowInteractable(bool canUp, bool canDown)
        {
            if (upButton != null)
            {
                upButton.interactable = canUp;
            }
            if (downButton != null)
            {
                downButton.interactable = canDown;
            }
            return this;
        }

        public void BindCall(System.Action call)
        {
            onClickCall = call;
        }

        public void BindToggleCall(System.Action<int, bool> call)
        {
            onToggleCall = call;
            if (enableToggle != null)
            {
                enableToggle.onValueChanged.RemoveAllListeners();
                enableToggle.onValueChanged.AddListener((value) => { onToggleCall?.Invoke(targetIndex, value); });
            }
        }

        public void OnClick()
        {
            onClickCall?.Invoke();
        }
        public void SetPressd(bool b)
        {
            //pressImg.enabled = b;
        }

        public void SetOver(bool b)
        {
            // 只控制悬停高亮;上移/下移按钮的显隐由选中状态(SetSelected)决定
            overImg.enabled = b;
        }

        private void Update()
        {
            if (mod != null && mod.IsDownloading())
            {
                progressImg.enabled = true;
                progressImg.fillAmount = mod.loadProgress;
            }
            else
            {
                progressImg.enabled = false;
                progressImg.fillAmount = 0;
            }

            if (mod != null)
            {
                if (isPress)
                {
                    pressTime += Time.deltaTime;
                    if (pressTime > 2)
                    {
                        deleteBtn.gameObject.SetActive(true);
                        isPress = false;
                    }
                }
            }
        }

        public void OnDeleteMod()
        {
            if (mod != null)
            {
                ModManager.Instance.RemoveMod(mod);
            }
        }
    }
}
