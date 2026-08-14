using Sango.Core;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>
    /// 通用数值输入器窗口。
    /// 通过Slider拖动、加减按钮、最大/最小按钮、数字键盘等方式输入一个整数，
    /// 打开时由调用方限定最小值与最大值，最终通过回调返回确认结果。
    /// </summary>
    public class UICalculator : UGUIWindow
    {
        #region UI组件

        public Text titleText;
        public Text valueText;
        public Slider valueSlider;
        public Button minusButton;
        public Button plusButton;
        public Button maxButton;
        public Button minButton;
        public Button sureButton;
        public Button cancelButton;

        /// <summary>
        /// 数字键盘按钮，索引0-9对应数字0-9。
        /// 由外部手动绑定。
        /// </summary>
        public Button[] digitButtons = new Button[10];

        /// <summary>
        /// 00按钮，点击时在当前数值末尾追加两个零（例如5→500）。
        /// 由外部手动绑定。
        /// </summary>
        public Button digit00Button;

        /// <summary>
        /// 最大按钮上的文本标签，用于显示"最大"或对应数值。
        /// 由外部手动绑定。
        /// </summary>
        public Text maxButtonLabel;

        /// <summary>
        /// 最小按钮上的文本标签，用于显示"最小"或对应数值。
        /// 由外部手动绑定。
        /// </summary>
        public Text minButtonLabel;

        /// <summary>
        /// 后退按钮，点击时移除数值末尾一位数字（例如123→12）。
        /// 由外部手动绑定。
        /// </summary>
        public Button backButton;

        /// <summary>
        /// 清除按钮，点击时将当前数值重置为0。
        /// 由外部手动绑定。
        /// </summary>
        public Button clearButton;

        /// <summary>
        /// 变化信息文本控件，用于显示额外提示信息。
        /// 由外部手动绑定。
        /// </summary>
        public Text infoText;

        /// <summary>
        /// 变化信息显示代理。输入当前数值，返回要显示的文本字符串。
        /// 由外部设置以自定义信息显示逻辑。
        /// </summary>
        public Func<int, string> infoDisplayDelegate;

        #endregion

        #region 数据

        /// <summary>
        /// 当前数值。
        /// </summary>
        protected int currentValue = 0;

        /// <summary>
        /// 允许的最小值。
        /// </summary>
        protected int minValue = 0;

        /// <summary>
        /// 允许的最大值。
        /// </summary>
        protected int maxValue = int.MaxValue;

        /// <summary>
        /// 确认回调，参数为最终选择的数值。
        /// </summary>
        protected Action<int> onConfirm;

        /// <summary>
        /// 取消回调。
        /// </summary>
        protected Action onCancel;

        /// <summary>
        /// 是否在刷新显示时抑制Slider事件，避免循环触发。
        /// </summary>
        protected bool isUpdatingSlider = false;

        #endregion

        #region 生命周期

        protected override void Awake()
        {
            base.Awake();
            BindEvents();
        }

        public override void OnOpen(params object[] objects)
        {
            base.OnOpen(objects);
            ParseOpenArgs(objects);
            RefreshDisplay();
        }

        #endregion

        #region 参数解析

        /// <summary>
        /// 解析打开窗口时传入的参数。
        /// 支持格式：
        /// (string title, int current, int min, int max, Action&lt;int&gt; onConfirm, Action onCancel)
        /// (int current, int min, int max, Action&lt;int&gt; onConfirm, Action onCancel)
        /// (int current, int min, int max, Action&lt;int&gt; onConfirm)
        /// </summary>
        protected virtual void ParseOpenArgs(object[] objects)
        {
            // 默认值
            string title = null;
            int current = 0;
            int min = 0;
            int max = int.MaxValue;
            Action<int> confirm = null;
            Action cancel = null;

            if (objects != null && objects.Length > 0)
            {
                int index = 0;

                // 第一个参数如果是字符串，则作为标题
                if (objects[0] is string)
                {
                    title = (string)objects[0];
                    index++;
                }

                // current
                if (index < objects.Length && objects[index] is int)
                {
                    current = (int)objects[index];
                    index++;
                }

                // min
                if (index < objects.Length && objects[index] is int)
                {
                    min = (int)objects[index];
                    index++;
                }

                // max
                if (index < objects.Length && objects[index] is int)
                {
                    max = (int)objects[index];
                    index++;
                }

                // onConfirm
                if (index < objects.Length && objects[index] is Action<int>)
                {
                    confirm = (Action<int>)objects[index];
                    index++;
                }

                // onCancel
                if (index < objects.Length && objects[index] is Action)
                {
                    cancel = (Action)objects[index];
                    index++;
                }
            }

            Setup(title, current, min, max, confirm, cancel);
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 手动设置计算器参数，供非窗口打开方式或测试调用。
        /// </summary>
        /// <param name="title">窗口标题</param>
        /// <param name="current">当前数值</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="confirm">确认回调</param>
        /// <param name="cancel">取消回调</param>
        public void Setup(string title, int current, int min, int max, Action<int> confirm, Action cancel = null)
        {
            this.minValue = min;
            this.maxValue = System.Math.Max(min, max);
            this.currentValue = ClampValue(current);
            this.onConfirm = confirm;
            this.onCancel = cancel;
            maxButtonLabel.text = maxValue.ToString();
            minButtonLabel.text = minValue.ToString();
            if (!string.IsNullOrEmpty(title) && titleText != null)
            {
                titleText.text = title;
            }

            RefreshDisplay();
        }
       
        /// <summary>
        /// 绑定Slider及所有按钮事件。
        /// </summary>
        protected virtual void BindEvents()
        {
            // Slider
            if (valueSlider != null)
            {
                valueSlider.onValueChanged.RemoveAllListeners();
                valueSlider.onValueChanged.AddListener(OnSliderValueChanged);
            }

            // 加减按钮
            if (minusButton != null)
            {
                minusButton.onClick.RemoveAllListeners();
                minusButton.onClick.AddListener(OnMinusClicked);
            }
            if (plusButton != null)
            {
                plusButton.onClick.RemoveAllListeners();
                plusButton.onClick.AddListener(OnPlusClicked);
            }

            // 最大/最小按钮
            if (maxButton != null)
            {
                maxButton.onClick.RemoveAllListeners();
                maxButton.onClick.AddListener(OnMaxClicked);
            }
            if (minButton != null)
            {
                minButton.onClick.RemoveAllListeners();
                minButton.onClick.AddListener(OnMinClicked);
            }

            // 确定/取消按钮
            if (sureButton != null)
            {
                sureButton.onClick.RemoveAllListeners();
                sureButton.onClick.AddListener(OnSureClicked);
            }
            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveAllListeners();
                cancelButton.onClick.AddListener(OnCancelClicked);
            }

            // 数字键盘 0-9
            if (digitButtons != null)
            {
                for (int i = 0; i < digitButtons.Length && i < 10; i++)
                {
                    if (digitButtons[i] != null)
                    {
                        int digit = i; // 捕获当前值，避免闭包陷阱
                        digitButtons[i].onClick.RemoveAllListeners();
                        digitButtons[i].onClick.AddListener(() => OnDigitClicked(digit));
                    }
                }
            }

            // 00按钮
            if (digit00Button != null)
            {
                digit00Button.onClick.RemoveAllListeners();
                digit00Button.onClick.AddListener(OnDoubleZeroClicked);
            }

            // 后退按钮
            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(OnBackClicked);
            }

            // 清除按钮
            if (clearButton != null)
            {
                clearButton.onClick.RemoveAllListeners();
                clearButton.onClick.AddListener(OnClearClicked);
            }
        }

        #endregion

        #region 数值操作

        /// <summary>
        /// 将数值限制在[minValue, maxValue]范围内（确认时使用）。
        /// </summary>
        protected virtual int ClampValue(int value)
        {
            return System.Math.Max(minValue, System.Math.Min(maxValue, value));
        }

        /// <summary>
        /// 将数值限制在 maxValue 以内（输入过程中使用，不限制最小值）。
        /// </summary>
        protected virtual int ClampMax(int value)
        {
            return System.Math.Min(maxValue, value);
        }

        /// <summary>
        /// 修改当前数值并刷新显示。
        /// 输入过程中仅限制最大值，最小值在确认时钳制。
        /// </summary>
        protected virtual void SetValue(int value)
        {
            currentValue = ClampMax(value);
            RefreshDisplay();
        }

        /// <summary>
        /// 刷新标题、数值文本、Slider显示与变化信息。
        /// </summary>
        protected virtual void RefreshDisplay()
        {
            if (valueText != null)
            {
                valueText.text = currentValue.ToString();
            }

            if (valueSlider != null)
            {
                // 避免在设置Slider值时触发onValueChanged造成循环
                isUpdatingSlider = true;
                if (maxValue > minValue)
                {
                    valueSlider.value = (float)(currentValue - minValue) / (float)(maxValue - minValue);
                }
                else
                {
                    valueSlider.value = 0f;
                }
                isUpdatingSlider = false;
            }

            RefreshInfoText();
        }

        #endregion

        #region 事件响应

        /// <summary>
        /// Slider数值变化时，根据比例反算当前值。
        /// </summary>
        protected virtual void OnSliderValueChanged(float normalizedValue)
        {
            if (isUpdatingSlider)
            {
                return;
            }

            if (maxValue > minValue)
            {
                int newValue = minValue + (int)System.Math.Round((maxValue - minValue) * normalizedValue);
                currentValue = ClampValue(newValue);
            }
            else
            {
                currentValue = minValue;
            }

            if (valueText != null)
            {
                valueText.text = currentValue.ToString();
            }

            RefreshInfoText();
        }

        /// <summary>
        /// 减1按钮。
        /// </summary>
        protected virtual void OnMinusClicked()
        {
            SetValue(currentValue - 1);
        }

        /// <summary>
        /// 加1按钮。
        /// </summary>
        protected virtual void OnPlusClicked()
        {
            SetValue(currentValue + 1);
        }

        /// <summary>
        /// 最大按钮。
        /// </summary>
        protected virtual void OnMaxClicked()
        {
            SetValue(maxValue);
        }

        /// <summary>
        /// 最小按钮。
        /// </summary>
        protected virtual void OnMinClicked()
        {
            SetValue(minValue);
        }

        /// <summary>
        /// 确定按钮：钳制到min/max后调用确认回调并关闭窗口。
        /// </summary>
        protected virtual void OnSureClicked()
        {
            currentValue = ClampValue(currentValue);
            onConfirm?.Invoke(currentValue);
            Close();
        }

        /// <summary>
        /// 取消/返回按钮：调用取消回调并关闭窗口。
        /// </summary>
        protected virtual void OnCancelClicked()
        {
            onCancel?.Invoke();
            Close();
        }

        /// <summary>
        /// 数字键点击处理（0-9）。
        /// 将对应数字追加到当前数值末尾。
        /// 外部手动绑定示例：btn0.onClick.AddListener(() => OnDigitClicked(0));
        /// </summary>
        /// <param name="digit">要追加的数字（0-9）</param>
        public virtual void OnDigitClicked(int digit)
        {
            if (digit < 0 || digit > 9)
            {
                return;
            }

            // 防溢出，不做最小值限制（确认时统一钳制）
            long newValue = (long)currentValue * 10 + digit;
            SetValue((int)System.Math.Min(newValue, (long)int.MaxValue));
        }

        /// <summary>
        /// 00按钮点击处理。
        /// 在当前数值末尾一次性追加两个零（例如5→500）。
        /// 外部手动绑定示例：btn00.onClick.AddListener(OnDoubleZeroClicked);
        /// </summary>
        public virtual void OnDoubleZeroClicked()
        {
            // 防溢出，不做最小值限制（确认时统一钳制）
            long newValue = (long)currentValue * 100;
            SetValue((int)System.Math.Min(newValue, (long)int.MaxValue));
        }

        /// <summary>
        /// 后退按钮处理。
        /// 移除数值末尾一位数字（例如123→12，1→0）。
        /// 外部手动绑定示例：btnBack.onClick.AddListener(OnBackClicked);
        /// </summary>
        public virtual void OnBackClicked()
        {
            if (currentValue == 0)
            {
                return;
            }

            int newValue = currentValue / 10;
            SetValue(newValue);
        }

        /// <summary>
        /// 清除按钮处理。
        /// 将当前数值重置为0。
        /// 外部手动绑定示例：btnClear.onClick.AddListener(OnClearClicked);
        /// </summary>
        public virtual void OnClearClicked()
        {
            SetValue(0);
        }

        /// <summary>
        /// 刷新变化信息文本。
        /// 通过 <see cref="infoDisplayDelegate"/> 代理获取显示内容，
        /// 并将结果写入 <see cref="infoText"/> 控件。
        /// 外部可通过设置 infoDisplayDelegate 自定义信息展示逻辑。
        /// </summary>
        protected virtual void RefreshInfoText()
        {
            if (infoText != null && infoDisplayDelegate != null)
            {
                infoText.text = infoDisplayDelegate(currentValue);
            }
        }

        #endregion
    }
}
