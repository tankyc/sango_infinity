using Sango.Core;
using System.Collections.Generic;

namespace Sango.Render
{
    public class DiplomacyEvent : RenderEventBase
    {
        public Person person;
        public DiplomacyActionType actionType;
        public Force receiverForce;
        public City targetCity;
        public int resourceValue;
        public int captiveId;
        private bool success;

        /// <summary>
        /// 本次到达是否已完成外交结算（幂等保护）。
        ///
        /// 【结算为什么放在事件里而不是台词回调里】
        /// 原版使者到达的结算是同步的：先 try_ceasefire 拿结果，演出只是把结果播出来，
        /// 最后统一落账（s11_sys_person_order_diplomacy.cpp:1052, 1132-1165）。
        /// 本项目演出被对话收尾回调驱动，回调一旦被 GameDialog 丢弃，结算跟着丢。
        /// 改成"Enter 判定 + 立即结算，回调只负责播台词与 IsDone"后，
        /// 即使演出链断掉（watchdog 强制完成），数据侧也不缺账。
        /// </summary>
        private bool settled;
        float timer;
        /// <summary>
        /// 演出上限。台词链要玩家点击推进，正常十秒内看完；给 60s 是防
        /// "收尾回调被丢 → IsDone 永久 false → 回合推进停摆"的兜底线，
        /// 超时后走 OnForceComplete 兜底，数据已经结算过，只会跳过剩余演出。
        /// </summary>
        public override float MaxDuration => 60f;

        public void Init(Person person, DiplomacyActionType actionType, Force receiverForce, City targetCity, int resourceValue, int captiveId)
        {
            this.person = person;
            this.actionType = actionType;
            this.receiverForce = receiverForce;
            this.targetCity = targetCity;
            this.resourceValue = resourceValue;
            this.captiveId = captiveId;
            IsDone = false;
            success = false;
            settled = false;
        }

        public override bool Update(Scenario scenario, float deltaTime)
        {
            timer += deltaTime;
            if (timer > 1)
            {
                if (!Window.Instance.IsOpen("window_city_diplomacy_scene"))
                {
                    IsDone = true;
                    timer = 0;
                    return IsDone;
                }
            }
            return base.Update(scenario, deltaTime);
        }

        /// <summary>
        /// 幂等结算：外交结果落地 + 使者领回城任务。无论从哪条路径（正常回调 / watchdog 兜底）调用，只生效一次。
        /// </summary>
        void Settle(bool result)
        {
            if (settled) return;
            settled = true;
            success = result;

            GameSystem.GetSystem<DiplomacyManager>().ExecuteDiplomacyMission(result, person, actionType, receiverForce, resourceValue);
            // 完成任务，返回原城市
            person.SetMission(MissionType.PersonReturn, person.BelongCity);
        }

        /// <summary>
        /// 演出层判定：按原版三态出口折算成败。
        ///
        /// 原版 Debate 档语义（s11_sys_person_order_diplomacy.cpp:1077-1110）：
        ///   · 使者是玩家 → 进入舌战，赢才算成功；
        ///   · 其余（AI 之间）→ 直接判失败。
        /// 本项目外交演出未接舌战系统，Debate 无处可转，一律按失败处理；
        /// 旧实现把 Debate 折成 50% 概率掷骰（ToSuccessRate(Debate)=50），
        /// 相当于把"该进舌战"当概率抽签，与原版不符，已删除。
        /// </summary>
        bool ResolveCutsceneResult(DiplomacyActionBase action)
        {
            if (action == null) return false;

            action.CalculateSuccessRate();      // 填充 LastResult（原版 try_* 的三态出口）

            return action.LastResult == DiplomacyRules.Result.Success
                || action.LastResult == DiplomacyRules.Result.SuccessNarrow;
        }

        /// <summary>watchdog 兜底：演出链断掉时，未结算的外交按失败落地（使者同样领回城任务）。</summary>
        public override void OnForceComplete(Scenario scenario)
        {
            if (!settled)
            {
                Sango.Log.Error(
                    $"*[演出]DiplomacyEvent 超时兜底：{person?.Name} 对 {receiverForce?.Name} 的{actionType}谈判未结算，按失败落地");
                Settle(false);
            }
        }

        public override void Exit(Scenario scenario)
        {
            Window.Instance.Close("window_city_diplomacy_scene");
        }

        public override void Enter(Scenario scenario)
        {
            timer = 0;
            if (person.IsPlayer || receiverForce.IsPlayer)
            {
                Window.Instance.Open("window_city_diplomacy_scene");
                switch (actionType)
                {
                    case DiplomacyActionType.Alliance:
                        {
                            List<GameDialog.TalkData> talkDatas = new List<GameDialog.TalkData>
                            {
                                new GameDialog.TalkData
                                {
                                    person = receiverForce.mGovernor,
                                    text = $"{person.BelongForce.ColorName}军的{person.ColorName}远道而来辛苦了。\n立刻让我听听你的来意吧！",
                                },
                                    new GameDialog.TalkData
                                {
                                    person = person,
                                    text = $"此次前来不为其他。\n是想请问可否与我方{person.BelongForce.ColorName}军\n缔结同盟呢？",
                                },
                                new GameDialog.TalkData
                                {
                                    person = receiverForce.mGovernor,
                                    text = $"什么，同盟……？\n嗯嗯……",
                                }
                            };

                            if (receiverForce.IsPlayer)
                            {
                                // 玩家选择结果。结算在玩家点下按钮的那一刻立即落地（原版 :1073-1076
                                // ceasefire_dialog 同步选择后结算），第二段台词只负责演出
                                GameDialog.StartTalk(talkDatas, () =>
                                {
                                    System.Action sureAction = () =>
                                    {
                                        Settle(true);

                                        talkDatas.Clear();
                                        talkDatas.Add(new GameDialog.TalkData
                                        {
                                            person = receiverForce.mGovernor,
                                            text = $"那么就和{person.BelongForce.ColorName}军缔结同盟吧。",
                                        });
                                        GameDialog.StartTalk(talkDatas, () => { IsDone = true; });
                                    };
                                    System.Action cancelAction = () =>
                                    {
                                        Settle(false);

                                        talkDatas.Clear();
                                        talkDatas.Add(new GameDialog.TalkData
                                        {
                                            person = receiverForce.mGovernor,
                                            text = $"{person.BelongForce.ColorName}军缔结同盟，\n毕竟是不可能的事。\n就当做没有过这回事好了。",
                                        });
                                        GameDialog.StartTalk(talkDatas, () => { IsDone = true; });
                                    };
                                    Window.Instance.Open("window_city_diplomacy_player_check_dialog", actionType, person, receiverForce, resourceValue, sureAction, cancelAction);
                                });
                            }
                            else
                            {
                                DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
                                // 创建外交行为实例。判定在 Enter 时立即完成并结算（原版 :1052 先 try_* 再演出），
                                // 台词只播结果，不再把结算压到收尾回调里
                                DiplomacyActionBase action = diplomacyManager.CreateDiplomacyAction(actionType, person.BelongForce, receiverForce, person, resourceValue);
                                bool success = ResolveCutsceneResult(action);
                                Settle(success);

                                DelayEvent delay = RenderEvent.Instance.Create<DelayEvent>();
                                delay.Init(1, () =>
                                {
                                    if (!success)
                                    {
                                        talkDatas.Add(new GameDialog.TalkData
                                        {
                                            person = receiverForce.mGovernor,
                                            text = $"{person.BelongForce.ColorName}军缔结同盟，\n毕竟是不可能的事。\n就当做没有过这回事好了。",
                                        });
                                    }
                                    else
                                    {
                                        talkDatas.Add(new GameDialog.TalkData
                                        {
                                            person = receiverForce.mGovernor,
                                            text = $"那么就和{person.BelongForce.ColorName}军缔结同盟吧。",
                                        });
                                    }

                                    GameDialog.StartTalk(talkDatas, () => { IsDone = true; });
                                });
                                RenderEvent.Instance.Add(delay);
                            }
                        }
                        break;
                    case DiplomacyActionType.Truce:
                        PlayAutoNegotiation(
                            $"{person.BelongForce.ColorName}军的{person.ColorName}，此来何意？",
                            "连年征战，两败俱伤。\n我家主公愿与贵军休兵停战，不知意下如何？",
                            "也罢，就暂且休兵吧。",
                            "停战？恕难从命。");
                        break;
                    case DiplomacyActionType.DeclareWar:
                        // 宣战恒成功（Action 的成功率固定 100），这里仍然走同一条演出/结算链
                        PlayAutoNegotiation(
                            $"{person.BelongForce.ColorName}军的{person.ColorName}，有何贵干？",
                            "我代表我家主公前来通告：\n自今日起，我方与贵军断绝往来！",
                            "既然如此，战场上见吧。",
                            "哦？那便拭目以待。");
                        break;
                    case DiplomacyActionType.Annex:
                        PlayAutoNegotiation(
                            $"{person.BelongForce.ColorName}军的{person.ColorName}，忽然造访所为何事？",
                            "贵军已无回天之力。\n若肯归顺我方，可保全军上下性命无虞。",
                            "……罢了，我等愿降。",
                            "宁死不降！请回吧。");
                        break;
                    case DiplomacyActionType.Exchange:
                        PlayAutoNegotiation(
                            $"{person.BelongForce.ColorName}军的{person.ColorName}，来谈什么？",
                            "两家各有俘获。\n不如将俘虏各自交还，免得将士埋骨他乡。",
                            "此言有理，就此交换吧。",
                            "俘虏之事，不必再提。");
                        break;
                    case DiplomacyActionType.Reinforce:
                        PlayAutoNegotiation(
                            $"{person.BelongForce.ColorName}军的{person.ColorName}，可是有求于我？",
                            "我军正与强敌苦战，\n恳请贵军念在同盟之谊，发兵相助！",
                            "既是同盟，自当相助。",
                            "我军自顾不暇，恕难从命。");
                        break;
                    case DiplomacyActionType.SendGift:
                        {
                            // 送礼总是成功：结算先行（同 PlayAutoNegotiation 的理由），台词只演出
                            Settle(true);

                            DelayEvent delay = RenderEvent.Instance.Create<DelayEvent>();
                            delay.Init(1, () =>
                            {
                                List<GameDialog.TalkData> talkDatas = new List<GameDialog.TalkData>
                                {
                                new GameDialog.TalkData
                                {
                                    person = receiverForce.mGovernor,
                                    text = $"{person.ColorName},别来无恙!",
                                },
                                 new GameDialog.TalkData
                                {
                                    person = person,
                                    text = $"我代表我家主公{person.BelongForce.mGovernor.ColorName}向您呈上最真挚的情谊,此次携带1000金,还请{receiverForce.mGovernor.ColorName}笑纳!",
                                },
                                 new GameDialog.TalkData
                                {
                                     person = receiverForce.mGovernor,
                                    text = $"既然{person.BelongForce.mGovernor.ColorName}这么有心意,那我就收下了!!!",
                                },
                                };
                                GameDialog.StartTalk(talkDatas, () => { IsDone = true; });
                            });
                            RenderEvent.Instance.Add(delay);
                        }
                        break;
                    // 请求技术 / 请求兵力 / 通商 / 和亲 / 请求结盟 / 请求停战 / 赎回俘虏：
                    // 原版没有这些命令（7 个使者命令见 s11_sys_person_order_diplomacy.cpp），
                    // 对应枚举项与演出分支已删除。
                    default:
                        break;
                }

                //if (receiverForce.IsPlayer)
                //{
                //    // 计算成功率
                //    int successRate = GameSystem.GetSystem<DiplomacyManager>().CalculateDiplomacySuccessRate(actionType, person.BelongForceId, receiverForce, person, resourceValue);
                //    success = GameRandom.Chance(successRate);


                //}
                //else
                //{
                //    // 计算成功率
                //    int successRate = GameSystem.GetSystem<DiplomacyManager>().CalculateDiplomacySuccessRate(actionType, person.BelongForceId, receiverForce, person, resourceValue);
                //    success = GameRandom.Chance(successRate);
                //    switch (actionType)
                //    {
                //        case DiplomacyActionType.Alliance:
                //            // 关系值越高，成功率越高，最高90%
                //            break;
                //        case DiplomacyActionType.Truce:
                //            // 关系值越高，成功率越高，最高80%
                //            break;
                //        case DiplomacyActionType.DeclareWar:
                //            // 宣战总是成功
                //            break;
                //        case DiplomacyActionType.SendGift:
                //            DelayEvent delay = RenderEvent.Instance.Create<DelayEvent>();
                //            delay.Init(1);
                //            RenderEvent.Instance.Add(delay);
                //            List<GameDialog.TalkData> talkDatas = new List<GameDialog.TalkData>
                //            {
                //                new GameDialog.TalkData
                //                {
                //                    person = receiverForce.Governor,
                //                    text = $"{person.ColorName},别来无恙!",
                //                },
                //                 new GameDialog.TalkData
                //                {
                //                    person = person,
                //                    text = $"我代表我家主公{person.BelongForce.Governor.ColorName}向您呈上最真挚的情谊,此次携带1000金,还请{person.ColorName}笑纳!",
                //                },
                //                 new GameDialog.TalkData
                //                {
                //                     person = receiverForce.Governor,
                //                    text = $"既然{person.BelongForce.Governor.ColorName}这么有心意,那我就收下了!!!",
                //                },
                //            };

                //            GameDialog.StartTalk(talkDatas, () => { IsDone = true; });
                //            // 送礼总是成功
                //            break;
                //        case DiplomacyActionType.RequestTechnique:
                //            // 关系值越高，成功率越高，最高85%
                //            break;
                //        case DiplomacyActionType.RequestTroops:
                //            // 关系值越高，成功率越高，最高80%
                //            break;
                //        case DiplomacyActionType.Trade:
                //            // 关系值越高，成功率越高，最高95%
                //            break;
                //        case DiplomacyActionType.Marriage:
                //            // 关系值越高，成功率越高，最高95%
                //            break;
                //        case DiplomacyActionType.AllianceRequest:
                //            // 关系值越高，成功率越高，最高85%
                //            break;
                //        case DiplomacyActionType.TruceRequest:
                //            // 关系值越高，成功率越高，最高75%
                //            break;
                //        case DiplomacyActionType.Ransom:
                //            // 关系值越高，成功率越高，最高90%
                //            break;
                //        default:
                //            break;
                //    }

                //}
            }
            else
            {
                GameSystem.GetSystem<DiplomacyManager>().ExecuteDiplomacyMission(person, actionType, receiverForce, resourceValue);
                // 完成任务，返回原城市
                person.SetMission(MissionType.PersonReturn, person.BelongCity);
                IsDone = true;
            }
        }

        /// <summary>
        /// 通用的外交谈判演出。
        ///
        /// 适用于除"送礼 / 结盟"之外的所有命令（停战、宣战、劝降、俘虏交换、请求援军），
        /// 它们的分支结构完全一致，只是台词不同：
        ///   · 目标是玩家 → 弹 `window_city_diplomacy_player_check_dialog`，由玩家点同意 / 拒绝；
        ///   · 目标是 AI   → 用 <see cref="DiplomacyManager.CalculateDiplomacySuccessRate"/> 判成败，
        ///                   播对应结果台词后调 <see cref="DiplomacyManager.ExecuteDiplomacyMission"/>。
        ///
        /// 【为什么不再各写一遍】旧实现里 7 个 case 全是空注释（`// 关系值越高…`），
        /// 使者抵达后既不演出也不结算，命令等于没生效。收敛到本方法后，
        /// 后续新增命令只要给一句台词即可。
        /// </summary>
        /// <param name="openText">对方开场白</param>
        /// <param name="proposeText">使者提出请求</param>
        /// <param name="acceptText">对方答应</param>
        /// <param name="refuseText">对方拒绝</param>
        void PlayAutoNegotiation(string openText, string proposeText, string acceptText, string refuseText)
        {
            List<GameDialog.TalkData> talkDatas = new List<GameDialog.TalkData>
            {
                new GameDialog.TalkData { person = receiverForce.mGovernor, text = openText },
                new GameDialog.TalkData { person = person, text = proposeText },
            };

            // 目标是玩家：交给玩家决定。结算在玩家点下按钮的那一刻立即落地
            //（原版 target 为玩家时走 ceasefire_dialog 同步选择，:1073-1076），台词只负责演出
            if (receiverForce.IsPlayer)
            {
                GameDialog.StartTalk(talkDatas, () =>
                {
                    System.Action sureAction = () =>
                    {
                        Settle(true);

                        talkDatas.Clear();
                        talkDatas.Add(new GameDialog.TalkData { person = receiverForce.mGovernor, text = acceptText });
                        GameDialog.StartTalk(talkDatas, () => { IsDone = true; });
                    };

                    System.Action cancelAction = () =>
                    {
                        Settle(false);

                        talkDatas.Clear();
                        talkDatas.Add(new GameDialog.TalkData { person = receiverForce.mGovernor, text = refuseText });
                        GameDialog.StartTalk(talkDatas, () => { IsDone = true; });
                    };

                    Window.Instance.Open("window_city_diplomacy_player_check_dialog",
                        actionType, person, receiverForce, resourceValue, sureAction, cancelAction);
                });
                return;
            }

            // 目标是 AI：Enter 时立即按原版三态判定并结算（见 ResolveCutsceneResult），
            // 台词只播结果。旧实现在这里 GameRandom.Chance 掷骰且把结算压进收尾回调，
            // 是"拒绝后回合卡死"的两个根因之一
            DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
            DiplomacyActionBase action = diplomacyManager.CreateDiplomacyAction(
                actionType, person.BelongForce, receiverForce, person, resourceValue);

            bool success = ResolveCutsceneResult(action);
            Settle(success);

            DelayEvent delay = RenderEvent.Instance.Create<DelayEvent>();
            delay.Init(1, () =>
            {
                talkDatas.Add(new GameDialog.TalkData
                {
                    person = receiverForce.mGovernor,
                    text = success ? acceptText : refuseText,
                });

                GameDialog.StartTalk(talkDatas, () => { IsDone = true; });
            });
            RenderEvent.Instance.Add(delay);
        }
    }
}
