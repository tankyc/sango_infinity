using System;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    /// <summary>
    /// 对象选择系统通用接口 - 供UIDataEdit按数据集类型统一调用对应的对象选择器
    /// 各具体选择系统（武将/势力/城池/军团/特技等）通过显式实现本接口暴露统一的启动方法，
    /// 使数据编辑器无需关心具体类型即可打开单选或多选选择器
    /// </summary>
    /// <typeparam name="T">可选对象类型</typeparam>
    public interface IObjectSelectSystem<T> where T : SangoObject
    {
        /// <summary>
        /// 启动对象选择器
        /// </summary>
        /// <param name="candidates">候选对象列表</param>
        /// <param name="resultList">初始已选对象列表</param>
        /// <param name="limit">选择数量上限（1为单选）</param>
        /// <param name="action">选择完成回调</param>
        /// <param name="customSortTitles">自定义排序标题列表（为空时使用选择系统默认列表）</param>
        /// <param name="cutomSortTitleName">自定义排序标题组名称</param>
        void Start(List<T> candidates, List<T> resultList, int limit, Action<List<T>> action, List<ObjectSortTitle> customSortTitles, string cutomSortTitleName);
    }
}
