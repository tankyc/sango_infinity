using Sango.Core.Player;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Sango.Core
{
   
    public class FeatureSortFunction : Singleton<FeatureSortFunction>
    {
        public delegate string FeatureValueStrGet(Feature Feature);
        public delegate int FeatureValueGet(Feature Feature);
        public delegate int FeatureSortFunc(Feature Feature1, Feature Feature2);

        /// <summary>
        /// 获取Feature对象属性值的object类型代理
        /// </summary>
        /// <param name="Feature">武将对象</param>
        /// <returns>属性值</returns>
        public delegate object FeatureValueObjGet(Feature Feature);

        /// <summary>
        /// 设置Feature对象属性值的代理
        /// </summary>
        /// <param name="Feature">武将对象</param>
        /// <param name="value">新的属性值</param>
        public delegate void FeatureValueObjSet(Feature Feature, object value);

        public class SortTitle : ObjectSortTitle
        {
            public FeatureValueStrGet valueGetCall;
            public FeatureSortFunc FeatureSortFunc;
            public FeatureValueObjGet valueObjGet;
            public FeatureValueObjSet valueObjSet;

            public override object GetValue(SangoObject obj)
            {
                return valueObjGet?.Invoke((Feature)obj);
            }

            public override void SetValue(SangoObject obj, object value)
            {
                valueObjSet?.Invoke((Feature)obj, value);
            }

            public override string GetValueStr(SangoObject obj)
            {
                return valueGetCall.Invoke((Feature)obj);
            }

            public override int Sort(SangoObject a, SangoObject b)
            {
                return FeatureSortFunc.Invoke((Feature)a, (Feature)b);
            }

            public SortTitle Copy()
            {
                return new SortTitle
                {
                    name = name,
                    alignment = alignment,
                    width = width,
                    valueGetCall = valueGetCall,
                    FeatureSortFunc = FeatureSortFunc,
                    valueObjGet = valueObjGet,
                    valueObjSet = valueObjSet,
                };
            }
        }

        public static SortTitle SortByName = new SortTitle()
        {
            name = "特技",
            width = 3.20f,
            valueGetCall = x => x.Name,
            FeatureSortFunc = (a, b) => a.Name.CompareTo(b.Name),
            valueObjGet = x => x.Name,
            valueObjSet = (x, v) => x.Name = (string)v,
        };


        public static SortTitle SortByDesc = new SortTitle()
        {
            name = "说明",
            width = 30.00f,
            valueGetCall = x => x.desc.ToString(),
            FeatureSortFunc = (a, b) => a.desc.CompareTo(b.desc),
            valueObjGet = x => x.desc,
            valueObjSet = null,
        };

        public static List<ObjectSortTitle> DefaultSortList = new List<ObjectSortTitle>
        {
            SortByName,
            SortByDesc,
        };

    }

}
