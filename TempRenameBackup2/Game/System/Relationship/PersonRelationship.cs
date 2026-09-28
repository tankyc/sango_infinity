using System.Collections.Generic;

namespace Sango.Core.Player
{
    /// <summary>
    /// 仲介类指令的抽象基类(结婚/结义)
    /// 本基类不注册为游戏模块, 只有派生类带[GameSystem]才会被创建
    /// 点击菜单后直接进入PersonSelectSystem选人,
    /// 校验提示走通用的GameDialog, 旁白与武将应答走带事件大图的对话弹窗
    /// </summary>
    public abstract class PersonRelationship : CityBaseSystem
    {
        /// <summary>
        /// 本次指令的候选武将列表
        /// </summary>
        protected List<Person> candidates = new List<Person>();

        /// <summary>
        /// 指令标题, 同时作为选择列表的标题
        /// </summary>
        protected abstract string RelationshipTitle { get; }

        /// <summary>
        /// 菜单路径, 形如"君主/仲介/结婚"
        /// </summary>
        protected abstract string RelationshipMenu { get; }

        /// <summary>
        /// 菜单排序
        /// </summary>
        protected abstract int RelationshipMenuOrder { get; }

        /// <summary>
        /// 最多可选人数(结婚2人, 结义3人)
        /// </summary>
        protected abstract int SelectLimit { get; }

        /// <summary>
        /// 消耗的技巧点(势力资源 Force.TechniquePoint)
        /// </summary>
        protected virtual int TechniqueCost => 500;

        /// <summary>
        /// 已执行成功, 不再需要重新选人
        /// </summary>
        private bool finished;

        /// <summary>
        /// 校验不通过, 等待玩家重新选择
        /// </summary>
        private bool relaunching;

        /// <summary>
        /// 已选好人选, 等待人物应答弹窗结束
        /// </summary>
        private bool confirming;

        /// <summary>
        /// 子类过滤候选武将(决定谁有资格进入选人列表)
        /// </summary>
        protected abstract bool FilterCandidate(Person person);

        /// <summary>
        /// 勾选联动过滤: 根据已经勾选的武将决定其他人当下是否显示,
        /// 取消勾选后自动恢复; 基类统一排除血缘亲亲和厌恶对象,
        /// 子类叠加自己的规则
        /// </summary>
        protected virtual bool FilterBySelected(SangoObject target, List<SangoObject> selected)
        {
            Person person = target as Person;
            if (person == null || selected == null) return true;

            for (int i = 0; i < selected.Count; i++)
            {
                Person other = selected[i] as Person;
                if (other == null) continue;

                // 与已勾选人有任何血缘关系的一律隐藏(父母子女/兄弟姐妹/祖孙/叔侄/堂表)
                if (IsBloodRelative(person, other))
                    return false;

                // 只要有一方厌恶对方就不再显示
                if (IsHated(person, other))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 两人是否有血缘关系, 直接用Person.IsBloodRelative:
        /// 父母子女(含过继) / 同父或同母的兄弟姐妹 / 三代以内有共同祖先
        /// </summary>
        protected static bool IsBloodRelative(Person a, Person b)
        {
            if (a == null || b == null) return false;
            return a.IsBloodRelative(b);
        }

        /// <summary>
        /// 两人之间是否存在厌恶关系(单向也算)
        /// IsHate 读的是 HatePersonList, 只表示"a厌恶b", 所以要双向判断
        /// </summary>
        protected static bool IsHated(Person a, Person b)
        {
            if (a == null || b == null) return false;
            return a.IsHate(b) || b.IsHate(a);
        }

        /// <summary>
        /// 在人选中找出一对被禁止缔结关系的人并给出提示文案;
        /// 用于兜底(如"一并"一次选满时来不及联动过滤)
        /// </summary>
        protected static bool FindForbiddenPair(List<Person> picked, string title, out string errorContent)
        {
            for (int i = 0; i < picked.Count; i++)
            {
                for (int j = i + 1; j < picked.Count; j++)
                {
                    Person a = picked[i];
                    Person b = picked[j];
                    if (a == null || b == null) continue;

                    if (a.IsParentchild(b))
                    {
                        errorContent = $"{a.Name}与{b.Name}是父母子女关系, 不能{title}!";
                        return true;
                    }

                    if (IsBloodRelative(a, b))
                    {
                        errorContent = $"{a.Name}与{b.Name}有血缘关系, 不能{title}!";
                        return true;
                    }

                    bool aHateB = a.IsHate(b);
                    bool bHateA = b.IsHate(a);
                    if (aHateB && bHateA)
                    {
                        errorContent = $"{a.Name}与{b.Name}互相厌恶, 不能{title}!";
                        return true;
                    }
                    if (aHateB)
                    {
                        errorContent = $"{a.Name}厌恶{b.Name}, 不能{title}!";
                        return true;
                    }
                    if (bHateA)
                    {
                        errorContent = $"{b.Name}厌恶{a.Name}, 不能{title}!";
                        return true;
                    }
                }
            }

            errorContent = null;
            return false;
        }

        /// <summary>
        /// 校验玩家的选择结果, 不通过时给出提示文案
        /// </summary>
        protected abstract bool Validate(List<Person> selected, out string errorContent);

        /// <summary>
        /// 人选确定后, 每位当事武将依次开口的台词(一人一个弹窗);
        /// 返回空表示该武将不开口, 全部为空则直接缔结关系
        /// </summary>
        protected abstract string GetTalkContent(Person speaker, List<Person> selected);

        /// <summary>
        /// 开口顺序, 默认按玩家的选择顺序
        /// </summary>
        protected virtual List<Person> GetTalkOrder(List<Person> selected)
        {
            return selected;
        }

        /// <summary>
        /// 应答弹窗上显示的事件大图, 在所有对话之前就出现
        /// </summary>
        protected abstract string TalkImage(List<Person> selected);

        /// <summary>
        /// 排在所有武将应答之前的一句旁白, 返回空表示没有旁白
        /// </summary>
        protected virtual string GetNarration(List<Person> selected)
        {
            return null;
        }

        /// <summary>
        /// 建立关系
        /// </summary>
        protected abstract void Execute(List<Person> selected);

        protected PersonRelationship()
        {
            // 本类指令不依赖任何窗口
            windowName = "";
        }

        /// <summary>
        /// 两种仲介指令共用的列; 配偶与兄弟列默认都不显示,
        /// 需要时由子类重写本方法叠加
        /// </summary>
        protected virtual List<ObjectSortTitle> BuildTitleList()
        {
            return new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.SortBySex,
                PersonSortFunction.SortByAge,
                PersonSortFunction.SortByBelongCity,
                PersonSortFunction.SortByBelongCorps,
                PersonSortFunction.SortByLoyalty,
                Wider(PersonSortFunction.SortByFather, ParentNameWidth),
                Wider(PersonSortFunction.SortByMother, ParentNameWidth),
            };
        }

        /// <summary>
        /// 父/母列要能容纳四个字的名字, 与"武将"列同宽
        /// </summary>
        const float ParentNameWidth = 4.20f;

        /// <summary>
        /// 克隆一份列定义再改宽度,
        /// 不影响其他界面共用的静态列(SortByFather/SortByMother)
        /// </summary>
        protected static ObjectSortTitle Wider(PersonSortFunction.SortTitle title, float width)
        {
            PersonSortFunction.SortTitle copy = title.Copy();
            copy.width = width;
            return copy;
        }

        /// <summary>
        /// 菜单信息在Init时填充, 避免在构造函数里调用抽象成员
        /// </summary>
        public override void Init()
        {
            customTitleName = RelationshipTitle;
            customMenuName = RelationshipMenu;
            customMenuOrder = RelationshipMenuOrder;
            customTitleList = BuildTitleList();
            base.Init();
        }

        /// <summary>
        /// 返回false时对应的右键菜单项会置灰且不可点击
        /// (技巧点不足 或 符合基本条件的武将不够两人)
        /// </summary>
        public override bool IsValid
        {
            get
            {
                if (TargetCity == null || TargetCity.BelongForce == null)
                    return false;

                if (TargetCity.BelongForce.TechniquePoint < TechniqueCost)
                    return false;

                return BuildCandidates() >= 2;
            }
        }

        /// <summary>
        /// 收集本势力符合要求的武将, 返回候选数量
        /// </summary>
        protected int BuildCandidates()
        {
            candidates.Clear();
            if (TargetCity == null || TargetCity.BelongForce == null)
                return 0;

            Force targetForce = TargetCity.BelongForce;
            Scenario.Cur.personSet.ForEach(x =>
            {
                if (x.BelongForce != targetForce) return;
                if (x.IsDead || x.IsPrisoner) return;
                if (!FilterCandidate(x)) return;
                candidates.Add(x);
            });

            return candidates.Count;
        }

        public override void OnEnter()
        {
            finished = false;
            relaunching = false;
            confirming = false;
            BuildCandidates();
            StartSelect();
        }

        /// <summary>
        /// 拉起系统通用的武将选择列表
        /// </summary>
        void StartSelect()
        {
            personList.Clear();
            PersonSelectSystem selectSystem = GameSystem.GetSystem<PersonSelectSystem>();
            if (selectSystem == null)
            {
                Finish();
                return;
            }
            selectSystem.Start(candidates, personList, SelectLimit,
                OnPersonsSelected, customTitleList, customTitleName);

            // Start会重置过滤委托, 所以必须在Start之后挂上;
            // selected是同一个List实例, 闭包里每次读到的都是最新勾选状态
            selectSystem.displayFilter = obj => FilterBySelected(obj, selectSystem.selected);
        }

        void OnPersonsSelected(List<Person> selected)
        {
            // 空选视为放弃本次指令
            if (selected == null || selected.Count <= 0)
            {
                Finish();
                return;
            }

            // 兜底: 兼容"一并"等一次选满、来不及联动过滤的场合
            if (FindForbiddenPair(selected, RelationshipTitle, out string errorContent))
            {
                ShowError(errorContent);
                return;
            }

            if (!Validate(selected, out errorContent))
            {
                ShowError(errorContent);
                return;
            }

            List<GameDialog.TalkData> talks = BuildTalks(selected);
            if (talks.Count > 0)
            {
                // 大图在所有对话之前出现, 对话全部结束后才真正缔结关系
                confirming = true;
                ShowTalkWindow(selected, talks);
                return;
            }

            Commit(selected);
        }

        /// <summary>
        /// 仲介应答窗: 上方一张事件大图, 下方文字板逐句显示台词
        /// </summary>
        const string TalkWindowName = "window_relationship_talk";

        /// <summary>
        /// 拉起图片对话窗播放旁白与武将应答, 对话结束后才缔结关系
        /// </summary>
        void ShowTalkWindow(List<Person> selected, List<GameDialog.TalkData> talks)
        {
            Window.WindowInterface window = Window.Instance.Open(TalkWindowName, TalkImage(selected), talks);
            if (window == null || window.ugui_instance == null)
            {
                Commit(selected);
                return;
            }

            // 对话期间暂停游戏逻辑, 与GameDialog的表现保持一致
            GameController.Instance.Enabled = false;
            window.ugui_instance.OnCloseAction = () =>
            {
                window.ugui_instance.OnCloseAction = null;
                GameController.Instance.Enabled = true;
                Commit(selected);
            };
        }

        /// <summary>
        /// 收集旁白和每个人自己的台词, 生成图片对话窗的台词队列
        /// </summary>
        List<GameDialog.TalkData> BuildTalks(List<Person> selected)
        {
            List<GameDialog.TalkData> talks = new List<GameDialog.TalkData>();

            // 旁白没有说话人, 排在最前面, 大图就是在这句话出现之前显示的
            string narration = GetNarration(selected);
            if (!string.IsNullOrEmpty(narration))
                talks.Add(NewTalk(narration, null));

            List<Person> order = GetTalkOrder(selected);
            if (order == null || order.Count <= 0)
                return talks;

            for (int i = 0; i < order.Count; i++)
            {
                Person speaker = order[i];
                if (speaker == null) continue;

                string content = GetTalkContent(speaker, selected);
                if (string.IsNullOrEmpty(content)) continue;

                talks.Add(NewTalk(content, speaker));
            }
            return talks;
        }

        static GameDialog.TalkData NewTalk(string content, Person speaker)
        {
            GameDialog.TalkData talk = new GameDialog.TalkData();
            talk.text = content;
            talk.person = speaker;
            return talk;
        }

        /// <summary>
        /// 弹出错误提示, 关闭后重新拉起选人列表
        /// </summary>
        void ShowError(string content)
        {
            relaunching = true;
            // 无论确认还是取消都要重新拉起列表, 避免停在无界面的空指令上
            GameDialog.Instance.Open(GameDialog.DialogStyle.ClickSay, content, OnRelaunch, OnRelaunch);
        }

        /// <summary>
        /// 正式缔结关系并结算行动力
        /// </summary>
        void Commit(List<Person> selected)
        {
            confirming = false;
            if (finished) return;

            Execute(selected);

            // 扣势力技巧点, GainTechniquePoint 会同时通知UI刷新左上角数值
            Force force = TargetCity == null ? null : TargetCity.BelongForce;
            if (force != null)
                force.GainTechniquePoint(-TechniqueCost);

            GameMedia.Instance.PlayDoAcitonSfx();
            Finish();
        }

        /// <summary>
        /// 事件发生的时间, 形如"200年1月，"; 取不到剧本信息时为空
        /// </summary>
        protected static string DateLine
        {
            get
            {
                ScenarioInfo info = Scenario.Cur == null ? null : Scenario.Cur.Info;
                if (info == null) return "";
                return $"{info.year}年{info.month}月，";
            }
        }

        /// <summary>
        /// 用顿号拼接所有人选的名字
        /// </summary>
        protected static string JoinNames(List<Person> picked)
        {
            string names = "";
            for (int i = 0; i < picked.Count; i++)
            {
                if (picked[i] == null) continue;
                names += names.Length == 0 ? picked[i].Name : "、" + picked[i].Name;
            }
            return names;
        }

        /// <summary>
        /// 提示关闭后重新拉起选人列表
        /// </summary>
        void OnRelaunch()
        {
            // 保证只会重新拉起一次
            if (!relaunching || finished) return;
            relaunching = false;
            StartSelect();
        }

        void Finish()
        {
            finished = true;
            relaunching = false;
            confirming = false;

            // 选择器是全局共用实例, 必须解除过滤委托, 否则会影响到召唤/移动等指令
            PersonSelectSystem selectSystem = GameSystem.GetSystem<PersonSelectSystem>();
            if (selectSystem != null)
                selectSystem.ReleaseDisplayFilter();

            Done();
        }

        /// <summary>
        /// 从选人列表返回时:
        /// 校验失败的场合保持当前指令, 等对话框确认后重新选人;
        /// 等待武将应答的场合保持当前指令, 等应答全部结束后再缔结关系;
        /// 玩家取消选人的场合直接结束本指令
        /// </summary>
        public override void OnBack(ICommandEvent whoGone)
        {
            if (relaunching || confirming || finished) return;
            Finish();
        }

        /// <summary>
        /// 基类会去关闭一个空名字窗口, 这里屏蔽掉
        /// </summary>
        public override void OnDestroy()
        {
        }
    }
}
