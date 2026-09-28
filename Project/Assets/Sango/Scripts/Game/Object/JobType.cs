using Sango.Core.Tools;

namespace Sango.Core
{
    public class JobType : SangoObject
    {
        public string path;
        public int kind;
        public int costAP;
        public int cost;
        public int meritGain;
        public int tpGain;
        public int limit;
        public int[] recommandFeatures;

        public static int GetJobCost(int jobId)
        {
            JobType t = Scenario.Cur.CommonData.JobTypes.Get(jobId);
            if (t != null)
            {
                OverrideData<int> overrideData = Tools.OverrideData<int>.Create(t.cost);
                GameEvent.OnGetJobCost?.Invoke(t, t.cost, overrideData);
                return overrideData.ValueAndRecycle;
            }
            return 0;
        }
        public static int GetJobCostAP(int jobId)
        {

            JobType t = Scenario.Cur.CommonData.JobTypes.Get(jobId);
            if (t != null)
            {
                OverrideData<int> overrideData = Tools.OverrideData<int>.Create(t.costAP);
                GameEvent.OnGetJobCostAP?.Invoke(t, t.costAP, overrideData);
                return overrideData.ValueAndRecycle;
            }
            return 0;
        }
        public static int GetJobMeritGain(int jobId)
        {
            JobType t = Scenario.Cur.CommonData.JobTypes.Get(jobId);
            if (t != null)
            {
                OverrideData<int> overrideData = Tools.OverrideData<int>.Create(t.meritGain);
                // 这里以前误发 OnGetJobCost 并传 t.costAP：既让"工作消耗"的监听者改到了功绩收益，
                // 又使专属的 OnGetJobMeritGain 永远收不到通知。现在按语义发自己的事件。
                GameEvent.OnGetJobMeritGain?.Invoke(t, t.meritGain, overrideData);
                return overrideData.ValueAndRecycle;
            }
            return 0;
        }
        public static int GetJobTPGain(int jobId)
        {
            JobType t = Scenario.Cur.CommonData.JobTypes.Get(jobId);
            if (t != null)
            {
                OverrideData<int> overrideData = Tools.OverrideData<int>.Create(t.tpGain);
                // 同上：改用专属事件并传正确的原始值
                GameEvent.OnGetJobTPGain?.Invoke(t, t.tpGain, overrideData);
                return overrideData.ValueAndRecycle;
            }
            return 0;
        }
        public static int GetJobLimit(int jobId)
        {
            JobType t = Scenario.Cur.CommonData.JobTypes.Get(jobId);
            if (t != null) return t.limit;
            return 0;
        }
    }
}
