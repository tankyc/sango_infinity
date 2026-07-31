using System;
using System.Collections.Generic;

namespace Sango.Core
{
    public abstract class ObjectSortTitle
    {
        public string name;
        public float width;
        public int alignment = 4;
        public object customData;
        public float ContentMaxWidth { get { return width * 25f; } }

        public abstract string GetValueStr(SangoObject obj);
        public abstract int Sort(SangoObject a, SangoObject b);

        /// <summary>
        /// 获取属性值（通用object返回类型）
        /// 子类通过代理闭包实现具体逻辑
        /// </summary>
        /// <param name="obj">目标SangoObject</param>
        /// <returns>属性值</returns>
        public abstract object GetValue(SangoObject obj);

        /// <summary>
        /// 设置属性值（通用object参数类型）
        /// 子类通过代理闭包实现具体逻辑
        /// </summary>
        /// <param name="obj">目标SangoObject</param>
        /// <param name="value">新的属性值</param>
        public abstract void SetValue(SangoObject obj, object value);

        public ObjectSortTitle SetAlignment(int a) {  this.alignment = a; return this; }
        public ObjectSortTitle SetName(string a) {  this.name = a; return this; }
        public ObjectSortTitle SetCustomData(object a) {  this.customData = a; return this; }
    }
}
