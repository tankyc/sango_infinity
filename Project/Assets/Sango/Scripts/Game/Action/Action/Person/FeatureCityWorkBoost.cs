using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 内政工作速度加成（循吏 / 内政减费 / 设施专精）。
    ///
    /// <para>
    /// 订阅 <see cref="GameEvent.OnCityCalculateWorkSpeed"/>，按设施类别与执行人过滤后
    /// 修改 <see cref="CityWorkContext.speedFactor"/>。预览与实际提交共用同一事件，
    /// 因此界面显示与实扣一致。
    /// </para>
    /// <para>
    /// 参数：jobTypes（CityJobType 数组，空 = 全部内政）、speed（百分比加成，20 = +20%）、
    /// minTurns（耗时下限，交给后续工期系统使用）。
    /// </para>
    /// </summary>
    public class FeatureCityWorkBoost : ActionBase
    {
        /// <summary>作用的设施类别（空 = 全部）</summary>
        int[] jobTypes;
        /// <summary>速度百分比加成</summary>
        int speed;
        /// <summary>耗时下限（旬）</summary>
        int minTurns;
        /// <summary>装配宿主武将（用于只对本人执行的工作生效）</summary>
        Person ownerPerson;

        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            jobTypes = p.Value<JArray>("jobTypes")?.ToObject<int[]>();
            speed = p.Value<int>("speed");
            minTurns = p.Value<int>("minTurns");

            for (int i = 0; i < sangoObjects.Length; i++)
            {
                if (sangoObjects[i] is Person person)
                {
                    ownerPerson = person;
                    break;
                }
            }

            GameEvent.OnCityCalculateWorkSpeed += OnCityCalculateWorkSpeed;
        }

        /// <summary>退订事件（Action 被清理时必须调用）</summary>
        public override void Clear()
        {
            GameEvent.OnCityCalculateWorkSpeed -= OnCityCalculateWorkSpeed;
        }

        /// <summary>事件驱动效果，不需要外部主动触发。</summary>
        public override void Execute(Trigger trigger)
        {
        }

        /// <summary>工作速度结算：按设施类别 + 执行人过滤</summary>
        void OnCityCalculateWorkSpeed(CityWorkContext ctx)
        {
            if (ctx == null)
                return;

            if (jobTypes != null && jobTypes.Length > 0 && !ContainsJob(ctx.jobId))
                return;

            if (ownerPerson != null && !ContainsPerson(ctx.persons, ownerPerson))
                return;

            ctx.speedFactor += speed;
            if (minTurns > ctx.minTurns)
                ctx.minTurns = minTurns;
        }

        bool ContainsJob(int jobId)
        {
            for (int i = 0; i < jobTypes.Length; i++)
                if (jobTypes[i] == jobId)
                    return true;
            return false;
        }

        static bool ContainsPerson(Person[] persons, Person target)
        {
            if (persons == null)
                return false;
            for (int i = 0; i < persons.Length; i++)
                if (persons[i] == target)
                    return true;
            return false;
        }
    }
}
