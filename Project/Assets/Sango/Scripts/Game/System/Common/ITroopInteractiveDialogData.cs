using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sango.Core
{
    public interface ITroopInteractiveDialogData
    {
        
        public void Clear();
        public bool IsEmpty();
        public void SetContent(string content);

        public void SetSureAction(System.Action action);

        /// <summary>当前选项是否可执行（false 时内容仍显示，但确认按钮不响应）</summary>
        public bool IsValid { get; }

        /// <summary>设置可执行状态（条件不满足时置 false，实现"可见但置灰"）</summary>
        public void SetValid(bool valid);
    }
}
