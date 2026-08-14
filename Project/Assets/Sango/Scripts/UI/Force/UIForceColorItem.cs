using System;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>
    /// 势力颜色选择项。
    /// 显示单个旗帜颜色，并标识该颜色是否已被其他势力占用。
    /// </summary>
    public class UIForceColorItem : MonoBehaviour
    {
        #region UI组件

        /// <summary>
        /// 颜色图像，用于显示旗帜颜色。
        /// </summary>
        public Image colorImage;

        /// <summary>
        /// 遮罩图像，用于标识该颜色已被占用。
        /// </summary>
        public Image maskImage;

        /// <summary>
        /// 选择按钮。
        /// </summary>
        public Button selectButton;

        #endregion

        #region 数据

        /// <summary>
        /// 当前项对应的旗帜ID。
        /// </summary>
        public int FlagId { get; private set; }

        /// <summary>
        /// 当前项是否已被占用。
        /// </summary>
        public bool IsUsed { get; private set; }

        /// <summary>
        /// 点击回调，参数为旗帜ID。
        /// </summary>
        private Action<int> onClickCallback;

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化颜色项。
        /// </summary>
        /// <param name="flagId">旗帜ID</param>
        /// <param name="color">显示颜色</param>
        /// <param name="isUsed">是否已被占用</param>
        /// <param name="onClick">点击回调</param>
        public void SetData(int flagId, Color color, bool isUsed, Action<int> onClick)
        {
            FlagId = flagId;
            IsUsed = isUsed;
            onClickCallback = onClick;

            if (colorImage != null)
            {
                colorImage.color = color;
            }

            if (selectButton != null)
            {
                selectButton.interactable = !isUsed;
                selectButton.onClick.RemoveAllListeners();
                if (!isUsed)
                {
                    selectButton.onClick.AddListener(OnClick);
                }
            }

            if (maskImage != null)
            {
                maskImage.gameObject.SetActive(isUsed);
            }
        }

        #endregion

        #region 事件响应

        /// <summary>
        /// 选择按钮点击事件。
        /// </summary>
        private void OnClick()
        {
            if (IsUsed)
            {
                return;
            }

            onClickCallback?.Invoke(FlagId);
        }

        #endregion
    }
}
