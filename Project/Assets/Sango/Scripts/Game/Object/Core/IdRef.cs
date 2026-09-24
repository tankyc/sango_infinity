namespace Sango.Core
{
    /// <summary>
    /// "只存 id 的对象引用"的解析助手。
    ///
    /// 背景：对象引用以前靠 <see cref="Id2ObjConverter{T}"/> 把 id 写进存档、再在
    /// 全局 <c>GameEvent.OnScenarioPrepare</c> 里延迟回填对象。那条路有两个问题：
    ///   1. 解析发生在一次全局事件里，谁被解析、何时被解析都不直观；
    ///   2. 挂在<b>嵌套对象</b>或 <b>CommonData 数据库项</b>上时，
    ///      对象自身可能根本不在 Scenario 的 prepare 列表里，解析时机无法保证。
    ///
    /// 现在统一改成：<c>[JsonProperty("原名")] int XxxId</c> + 访问时按需解析（带缓存），
    /// 存档里就是纯数值，且不依赖任何 prepare 时序。写入时由 setter 同步 id。
    /// </summary>
    public static class IdRef
    {
        /// <summary>
        /// id → 对象；id &lt;= 0 或没有剧本上下文时返回 null
        /// （口径与 <see cref="Id2ObjConverter{T}.Id2Object{T}"/> 一致）。
        /// </summary>
        public static T Resolve<T>(int id) where T : SangoObject, new()
        {
            if (id <= 0) return null;
            Scenario scenario = Scenario.Cur;
            if (scenario == null) return null;
            return scenario.GetObject<T>(id);
        }
    }
}
