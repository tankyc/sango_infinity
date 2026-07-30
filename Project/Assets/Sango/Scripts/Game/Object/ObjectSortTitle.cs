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

        public ObjectSortTitle SetAlignment(int a) {  this.alignment = a; return this; }
        public ObjectSortTitle SetName(string a) {  this.name = a; return this; }
        public ObjectSortTitle SetCustomData(object a) {  this.customData = a; return this; }
    }
}
