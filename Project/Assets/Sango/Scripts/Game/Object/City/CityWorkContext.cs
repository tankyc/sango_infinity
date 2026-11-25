using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 内政工作上下文（预览 / 提交 / AI / 回合结算共用）。
    ///
    /// <para>
    /// 把"执行人、所在城、设施类别、耗时下限、资源来源、经验类别"收进一个对象，
    /// 避免每个内政 Action 各自拼参数、预览与实扣口径不一致。
    /// </para>
    /// <para>
    /// 【性能】低频路径（打开界面 / 点一次工作 / AI 决策），对象池复用；
    /// 无订阅者时 <see cref="CityWorkSystem.ApplyWorkSpeed"/> 直接返回原值，不分配。
    /// </para>
    /// </summary>
    public sealed class CityWorkContext
    {
        /// <summary>所在城市</summary>
        public City city;
        /// <summary>工作类型（CityJobType）</summary>
        public int jobId;
        /// <summary>执行人</summary>
        public Person[] persons;
        /// <summary>原始产出（未加速度修正）</summary>
        public int baseValue;
        /// <summary>速度百分比（100 = 正常）</summary>
        public int speedFactor;
        /// <summary>耗时下限（旬，至少 1）</summary>
        public int minTurns;
        /// <summary>是否预览（不实际扣资源 / 不加经验）</summary>
        public bool isPreview;

        static readonly Stack<CityWorkContext> pool = new Stack<CityWorkContext>(4);

        public static CityWorkContext Rent()
        {
            return pool.Count > 0 ? pool.Pop() : new CityWorkContext();
        }

        public static void Return(CityWorkContext ctx)
        {
            if (ctx == null)
                return;
            ctx.city = null;
            ctx.jobId = 0;
            ctx.persons = null;
            ctx.baseValue = 0;
            ctx.speedFactor = 100;
            ctx.minTurns = 1;
            ctx.isPreview = false;
            pool.Push(ctx);
        }
    }

    /// <summary>
    /// 内政工作速度 / 产出结算。
    ///
    /// <para>
    /// 【唯一口径】工作预览与实际提交都调用 <see cref="ApplyWorkSpeed"/>，
    /// 特技（循吏等）通过 <c>GameEvent.OnCityCalculateWorkSpeed</c> 修改速度，
    /// 保证"界面显示"与"实际扣减/产出"一致。
    /// </para>
    /// </summary>
    public static class CityWorkSystem
    {
        /// <summary>
        /// 对原始产出应用速度修正。
        /// </summary>
        /// <param name="city">城市</param>
        /// <param name="jobId">工作类型</param>
        /// <param name="persons">执行人</param>
        /// <param name="rawValue">原始产出</param>
        /// <returns>修正后的产出（至少 1 旬的保底由调用方按需处理）</returns>
        public static int ApplyWorkSpeed(City city, int jobId, Person[] persons, int rawValue)
        {
            if (rawValue <= 0 || GameEvent.OnCityCalculateWorkSpeed == null)
                return rawValue;

            CityWorkContext ctx = CityWorkContext.Rent();
            ctx.city = city;
            ctx.jobId = jobId;
            ctx.persons = persons;
            ctx.baseValue = rawValue;
            ctx.speedFactor = 100;
            ctx.minTurns = 1;

            GameEvent.OnCityCalculateWorkSpeed(ctx);

            int speed = ctx.speedFactor;
            CityWorkContext.Return(ctx);

            if (speed <= 0)
                return rawValue;
            if (speed == 100)
                return rawValue;

            int result = rawValue * speed / 100;
            return result > 0 ? result : 1;
        }
    }
}
