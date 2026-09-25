using System.Collections.Generic;
using System.Text;

namespace Sango.Core
{
    /// <summary>
    /// 岗位类型。数值顺序 = 默认优先级（小 = 优先满足），后两类为"内政工作族"。
    /// 以后要加"外交""计略"等岗位，只需在此加枚举 + 在编制规则里生成。
    /// </summary>
    public enum PostKind
    {
        /// <summary>守备：港关驻守（优先级最高）</summary>
        Garrison = 0,
        /// <summary>军事：出征 / 防守 / 镇压（按兵力决定数量）</summary>
        Military = 1,
        /// <summary>征兵：补充城内兵力</summary>
        RecruitTroops = 2,
        /// <summary>军备：兵装 / 器械 / 船</summary>
        Armament = 3,
        /// <summary>士气：训练</summary>
        TrainTroops = 4,
        /// <summary>搜索：寻找未发现 / 在野人才</summary>
        Search = 5,
        /// <summary>运输：搬运（输送队主将）</summary>
        Transport = 6,
        /// <summary>开发：农业 / 商业 / 技术（按内政建设情况决定数量）</summary>
        Develop = 7,
        /// <summary>后勤：治安 / 巡查</summary>
        Logistics = 8,
        /// <summary>
        /// 登用：招揽城内**在野**武将（`CityJobType.RecruitPerson`）。
        /// 与"搜索"是两个独立命令：搜索只负责"发现"（未发现 → 在野），登用负责"招揽"。
        /// </summary>
        RecruitPerson = 9,
    }

    /// <summary>
    /// 岗位能力权重向量（各项已按 0..1 归一化后使用）。
    /// </summary>
    public struct PostFit
    {
        /// <summary>统率权重</summary>
        public float command;
        /// <summary>武力权重</summary>
        public float strength;
        /// <summary>智力权重</summary>
        public float intelligence;
        /// <summary>政治权重</summary>
        public float politics;
        /// <summary>魅力权重</summary>
        public float glamour;
        /// <summary>搬运特性权重（HasFeatrue(8)）</summary>
        public float transport;
    }

    /// <summary>
    /// 一个岗位（编制单位）。区别于旧的"缺几人"标量：这里描述"缺什么样的人、为什么缺"。
    /// </summary>
    public struct Post
    {
        /// <summary>岗位类型</summary>
        public PostKind kind;
        /// <summary>同城内优先满足顺序（小=优先）</summary>
        public int priority;
        /// <summary>是否硬性（无人时必须给出原因）</summary>
        public bool required;
        /// <summary>能力权重</summary>
        public PostFit weights;
        /// <summary>所属城池 id</summary>
        public int cityId;
        /// <summary>所属城池名（仅用于报告）</summary>
        public string cityName;
        /// <summary>编制依据（为什么生成这个岗位，例如"征兵:兵力3200/上限10000(32%)"）</summary>
        public string trigger;
    }

    /// <summary>
    /// 岗位填充结果：该岗位由谁满足（本城在册 = local，或建议从外城调入）。
    /// </summary>
    public struct PostFilling
    {
        /// <summary>岗位</summary>
        public Post post;
        /// <summary>人选 id（0 = 空缺）</summary>
        public int personId;
        /// <summary>人名（仅用于报告）</summary>
        public string personName;
        /// <summary>匹配分</summary>
        public float score;
        /// <summary>是否由本城在册人员直接满足（false = 建议外调）</summary>
        public bool local;
        /// <summary>源城名（外调时非空；本城在岗为 null）—— 便于核对"人从哪来"</summary>
        public string fromCityName;
        /// <summary>原因链（"外调/前线 兵力24000/上限12000 内政6点 ..."）</summary>
        public string reason;
    }

    /// <summary>
    /// 一次部署求解的完整结果。**影子模式下只产出本对象，不执行任何调动**。
    /// </summary>
    public class DeploymentPlan
    {
        /// <summary>势力 id</summary>
        public int forceId;
        /// <summary>势力名</summary>
        public string forceName;
        /// <summary>调度作用域的军团 id（0 = 势力级作用域，无军团边界）</summary>
        public int corpsId;
        /// <summary>军团标签（如"第三军团"；势力级作用域为 null，仅用于报告显示）</summary>
        public string corpsName;
        /// <summary>是否"只算不调"（仅参考）：玩家直辖的第一军团为 true —— 报告里要标注清楚</summary>
        public bool adviceOnly;
        /// <summary>本势力可调动（空闲）人数</summary>
        public int personPool;
        /// <summary>全部岗位</summary>
        public List<Post> posts = new List<Post>();
        /// <summary>岗位填充结果（含本城在岗与建议外调）</summary>
        public List<PostFilling> fillings = new List<PostFilling>();
        /// <summary>未满足岗位的原因清单</summary>
        public List<string> unmet = new List<string>();

        /// <summary>建议外调数量（需要真正调动的人数）</summary>
        public int TransferCount()
        {
            int count = 0;
            for (int i = 0; i < fillings.Count; i++)
            {
                if (!fillings[i].local && fillings[i].personId > 0)
                    count++;
            }
            return count;
        }

        /// <summary>本城在岗满足数量</summary>
        public int LocalCount()
        {
            int count = 0;
            for (int i = 0; i < fillings.Count; i++)
            {
                if (fillings[i].local && fillings[i].personId > 0)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 生成人类可读报告（供影子/执行模式对拍与调参）。
        /// </summary>
        /// <param name="title">
        /// 首行标题前缀（如 "[部署执行] 本回合实际调动 13 人"）。
        /// 为空时用中性的 "[部署]" —— 避免在**执行模式**下被误标成"影子"。
        /// </param>
        public string Report(string title = null)
        {
            StringBuilder sb = new StringBuilder();

            // 真实空缺 = 岗位总数 − 在岗 − 建议外调。
            // 不再用 unmet.Count：那个集合里混着"被围 / 闸门否决 / 额度限流"等非岗位消息，会误导阅读。
            int vacant = posts.Count - LocalCount() - TransferCount();
            if (vacant < 0) vacant = 0;

            if (!string.IsNullOrEmpty(title))
                sb.Append(title).Append(" | ");
            else
                sb.Append("[部署] ");

            sb.Append('#').Append(forceId).Append(' ').Append(forceName);
            if (!string.IsNullOrEmpty(corpsName))
            {
                // 军团级作用域：明确指出调度边界；"仅参考"要标出来，
                // 否则会误以为这些建议已经下发（其实是玩家直辖军团，AI 只算不调）
                sb.Append(" · ").Append(corpsName);
                if (adviceOnly)
                    sb.Append("(仅参考)");
            }
            sb.Append(" 岗位:").Append(posts.Count)
              .Append(" 在岗:").Append(LocalCount())
              .Append(" 建议外调:").Append(TransferCount())
              .Append(" 空缺:").Append(vacant)
              .Append(" 可调动池:").Append(personPool)
              .AppendLine();

            // 岗型构成汇总：一眼看出"军事岗是否虚高"（按类型聚合成 军事(1)×276，不再一行刷几十遍）
            sb.Append("   岗位构成: ").Append(KindAggregate(posts, 0)).AppendLine();

            // 按城池聚合：岗位构成 / 在岗 / 建议外调 / 空缺 / 编制依据
            // 用"已输出城集合"去重（不再依赖 posts 的相邻顺序）：分阶段指派打乱顺序也不会重复打印
            HashSet<int> printedCities = new HashSet<int>();
            for (int i = 0; i < posts.Count; i++)
            {
                Post p = posts[i];
                if (!printedCities.Add(p.cityId))
                    continue;

                int postCount = 0, localCount = 0, transferCount = 0;
                for (int j = 0; j < posts.Count; j++)
                {
                    if (posts[j].cityId == p.cityId) postCount++;
                }
                for (int j = 0; j < fillings.Count; j++)
                {
                    if (fillings[j].post.cityId != p.cityId) continue;
                    if (fillings[j].local) localCount++;
                    else if (fillings[j].personId > 0) transferCount++;
                }

                int cityVacant = postCount - localCount - transferCount;
                if (cityVacant < 0) cityVacant = 0;

                sb.Append("  [").Append(p.cityName).Append("] 岗位").Append(postCount)
                  .Append(" 在岗").Append(localCount).Append(" 建议外调").Append(transferCount)
                  .Append(" 空缺").Append(cityVacant)
                  .Append(" | ").Append(KindAggregate(posts, p.cityId))
                  .AppendLine();

                // 编制依据（去重后输出，便于核对"为什么有这些岗位"）
                for (int j = 0; j < posts.Count; j++)
                {
                    if (posts[j].cityId != p.cityId) continue;
                    if (string.IsNullOrEmpty(posts[j].trigger)) continue;
                    if (j > 0 && posts[j - 1].cityId == p.cityId && posts[j - 1].trigger == posts[j].trigger) continue;
                    sb.Append("      原因: ").Append(KindName(posts[j].kind)).Append(" ← ")
                      .Append(posts[j].trigger).AppendLine();
                }
            }

            // 建议外调明细（带源城与原因链）
            for (int i = 0; i < fillings.Count; i++)
            {
                PostFilling f = fillings[i];
                if (f.local || f.personId <= 0) continue;
                sb.Append("   → ").Append(f.personName);
                if (!string.IsNullOrEmpty(f.fromCityName))
                    sb.Append('(').Append(f.fromCityName).Append(')');
                sb.Append(" ⇒ ").Append(f.post.cityName)
                  .Append(' ').Append(KindName(f.post.kind))
                  .Append(" 分数").Append(f.score.ToString("F2"))
                  .Append(" | ").Append(f.reason)
                  .AppendLine();
            }

            if (cityInfo != null && cityInfo.Count > 0)
            {
                sb.AppendLine("   —— 各城人力 vs 岗位（在册 / 空闲 / 在部队 / 在城 | 岗位 / 在岗 / 外调）——");
                for (int i = 0; i < cityInfo.Count; i++)
                    sb.Append("     ").Append(cityInfo[i]).AppendLine();
            }

            for (int i = 0; i < unmet.Count; i++)
                sb.Append("   ! ").Append(unmet[i]).AppendLine();

            return sb.ToString();
        }

        /// <summary>
        /// 【诊断】各城人力分布快照：`城名 在册N 空闲M 在部队K 在城(N-K)`。
        /// 用于区分"人不在城"与"人在城但被部队/任务占用"——这两者的处理路径完全不同。
        /// </summary>
        public List<string> cityInfo = new List<string>();

        /// <summary>
        /// 把岗位按"类型(优先级)"聚合成 <c>军事(1)×276 征兵(2)×12</c> 形式（保持首次出现顺序）。
        /// </summary>
        /// <param name="source">岗位表</param>
        /// <param name="cityId">只统计该城；≤0 = 全部城池</param>
        /// <returns>聚合文本（无岗位时为空串）</returns>
        public static string KindAggregate(List<Post> source, int cityId)
        {
            if (source == null || source.Count == 0)
                return "";

            List<string> keys = new List<string>();
            Dictionary<string, int> counts = new Dictionary<string, int>();
            for (int i = 0; i < source.Count; i++)
            {
                if (cityId > 0 && source[i].cityId != cityId) continue;
                string key = KindName(source[i].kind) + "(" + source[i].priority + ")";
                int c;
                if (counts.TryGetValue(key, out c))
                    counts[key] = c + 1;
                else
                {
                    counts[key] = 1;
                    keys.Add(key);
                }
            }

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < keys.Count; i++)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(keys[i]);
                if (counts[keys[i]] > 1) sb.Append('×').Append(counts[keys[i]]);
            }
            return sb.ToString();
        }

        /// <summary>岗位中文名（报告用）。</summary>
        public static string KindName(PostKind kind)
        {
            switch (kind)
            {
                case PostKind.Garrison: return "守备";
                case PostKind.RecruitTroops: return "征兵";
                case PostKind.Armament: return "军备";
                case PostKind.TrainTroops: return "训练";
                case PostKind.Search: return "搜索";
                case PostKind.Transport: return "运输";
                case PostKind.Develop: return "开发";
                case PostKind.Logistics: return "后勤";
                case PostKind.RecruitPerson: return "登用";
                default: return "军事";
            }
        }
    }
}
