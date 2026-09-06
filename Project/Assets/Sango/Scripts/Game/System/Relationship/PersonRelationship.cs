using System.Collections.Generic;

namespace Sango.Core.Player
{
    /// <summary>
    /// 仲介类指令的抽象基类(结婚/结义)
    /// 本基类不注册为游戏模块, 只有派生类带[GameSystem]才会被创建
    /// 点击菜单后直接进入PersonSelectSystem选人, 不新增任何窗口/预制体,
    /// 校验提示与武将应答都走通用的GameDialog
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
        /// 取消勾选后自动恢复; 基类统一排除血亲(父母/子女), 子类叠加自己的规则
        /// </summary>
        protected virtual bool FilterBySelected(SangoObject target, List<SangoObject> selected)
        {
            Person person = target as Person;
            if (person == null || selected == null) return true;

            for (int i = 0; i < selected.Count; i++)
            {
                // 已勾选任意一人的父母/子女不再可选
                if (IsBloodRelative(person, selected[i] as Person))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 是否直系血亲(父母/子女, 含过继等父子母子关系)
        /// IsParentchild 已覆盖双向: 对方是自己的父母, 或自己是对方的父母
        /// </summary>
        protected static bool IsBloodRelative(Person a, Person b)
        {
            if (a == null || b == null) return false;
            return a.IsParentchild(b);
        }

        /// <summary>
        /// 从已选的人选中找出一对父母/子女关系
        /// </summary>
        static bool FindBloodConflict(List<Person> picked, out Person a, out Person b)
        {
            for (int i = 0; i < picked.Count; i++)
            {
                for (int j = i + 1; j < picked.Count; j++)
                {
                    if (IsBloodRelative(picked[i], picked[j]))
                    {
                        a = picked[i];
                        b = picked[j];
                        return true;
                    }
                }
            }
            a = null;
            b = null;
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
                if (TargetCity == null || TargetCity.mBelongForce == null)
                    return false;

                if (TargetCity.mBelongForce.TechniquePoint < TechniqueCost)
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
            if (TargetCity == null || TargetCity.mBelongForce == null)
                return 0;

            Force targetForce = TargetCity.mBelongForce;
            Scenario.Cur.personSet.ForEach(x =>
            {
                if (x.mBelongForce != targetForce) return;
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

            Person conflictA, conflictB;
            if (FindBloodConflict(selected, out conflictA, out conflictB))
            {
                // 兼容"一并"等一次性选满的跳过联动过滤的场合
                ShowError($"{conflictA.Name}与{conflictB.Name}是父母子女关系, 不能{RelationshipTitle}!");
                return;
            }

            string errorContent;
            if (!Validate(selected, out errorContent))
            {
                ShowError(errorContent);
                return;
            }

            List<GameDialog.TalkData> talks = BuildTalks(selected);
            if (talks.Count > 0)
            {
                // 每位当事武将依次开口应答, 最后一个弹窗关闭后才真正缔结关系
                confirming = true;
                GameDialog.StartTalk(talks, () =>
                {
                    Commit(selected);
                });
                return;
            }

            Commit(selected);
        }

        /// <summary>
        /// 收集每个人自己的台词, 生成一人一个的应答弹窗数据
        /// </summary>
        List<GameDialog.TalkData> BuildTalks(List<Person> selected)
        {
            List<GameDialog.TalkData> talks = new List<GameDialog.TalkData>();

            List<Person> order = GetTalkOrder(selected);
            if (order == null || order.Count <= 0)
                return talks;

            for (int i = 0; i < order.Count; i++)
            {
                Person speaker = order[i];
                if (speaker == null) continue;

                string content = GetTalkContent(speaker, selected);
                if (string.IsNullOrEmpty(content)) continue;

                GameDialog.TalkData talk = new GameDialog.TalkData();
                talk.text = content;
                talk.person = speaker;
                talks.Add(talk);
            }
            return talks;
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
            Force force = TargetCity == null ? null : TargetCity.mBelongForce;
            if (force != null)
                force.GainTechniquePoint(-TechniqueCost);

            GameMedia.Instance.PlayDoAcitonSfx();
            Finish();
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
