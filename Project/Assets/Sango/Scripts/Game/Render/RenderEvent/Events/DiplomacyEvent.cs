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
        }
        public override void Exit(Scenario scenario)
        {
            Window.Instance.Close("window_city_diplomacy_scene");
            GameDialog.Close();
        }

        public override void Enter(Scenario scenario)
        {
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
                                    text = $"{person.mBelongForce.ColorName}军的{person.ColorName}远道而来辛苦了。\n立刻让我听听你的来意吧！",
                                },
                                    new GameDialog.TalkData
                                {
                                    person = person,
                                    text = $"此次前来不为其他。\n是想请问可否与我方{person.mBelongForce.ColorName}军\n缔结同盟呢？",
                                },
                                new GameDialog.TalkData
                                {
                                    person = receiverForce.mGovernor,
                                    text = $"什么，同盟……？\n嗯嗯……",
                                }
                            };

                            if (receiverForce.IsPlayer)
                            {
                                // 玩家选择结果
                                GameDialog.StartTalk(talkDatas, () =>
                                {
                                    System.Action sureAction = () =>
                                    {
                                        talkDatas.Clear();
                                        talkDatas.Add(new GameDialog.TalkData
                                        {
                                            person = receiverForce.mGovernor,
                                            text = $"那么就和{person.mBelongForce.ColorName}军缔结同盟吧。",
                                        });
                                        GameDialog.StartTalk(talkDatas, () =>
                                        {
                                            GameSystem.GetSystem<DiplomacyManager>().ExecuteDiplomacyMission(true, person, actionType, receiverForce, resourceValue);
                                            // 完成任务，返回原城市
                                            person.SetMission(MissionType.PersonReturn, person.mBelongCity);
                                            IsDone = true;
                                        });
                                    };
                                    System.Action cancelAction = () =>
                                    {
                                        talkDatas.Clear();
                                        talkDatas.Add(new GameDialog.TalkData
                                        {
                                            person = receiverForce.mGovernor,
                                            text = $"{person.mBelongForce.ColorName}军缔结同盟，\n毕竟是不可能的事。\n就当做没有过这回事好了。",
                                        });
                                        GameDialog.StartTalk(talkDatas, () =>
                                        {
                                            // 失败不需要执行外交任务
                                            GameSystem.GetSystem<DiplomacyManager>().ExecuteDiplomacyMission(false, person, actionType, receiverForce, resourceValue);

                                            // 完成任务，返回原城市
                                            person.SetMission(MissionType.PersonReturn, person.mBelongCity);
                                            IsDone = true;
                                        });
                                    };
                                    Window.Instance.Open("window_city_diplomacy_player_check_dialog", actionType, person, receiverForce, resourceValue, sureAction, cancelAction);
                                });
                            }
                            else
                            {
                                DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
                                // 创建外交行为实例并计算成功率
                                DiplomacyActionBase action = diplomacyManager.CreateDiplomacyAction(actionType, person.mBelongForce, receiverForce, person, resourceValue);
                                int successRate = diplomacyManager.CalculateDiplomacySuccessRate(action);
                                bool success = GameRandom.Chance(successRate);

                                DelayEvent delay = RenderEvent.Instance.Create<DelayEvent>();
                                delay.Init(1, () =>
                                {
                                    if (!success)
                                    {
                                        talkDatas.Add(new GameDialog.TalkData
                                        {
                                            person = receiverForce.mGovernor,
                                            text = $"{person.mBelongForce.ColorName}军缔结同盟，\n毕竟是不可能的事。\n就当做没有过这回事好了。",
                                        });
                                    }
                                    else
                                    {
                                        talkDatas.Add(new GameDialog.TalkData
                                        {
                                            person = receiverForce.mGovernor,
                                            text = $"那么就和{person.mBelongForce.ColorName}军缔结同盟吧。",
                                        });
                                    }

                                    GameDialog.StartTalk(talkDatas, () =>
                                    {
                                        diplomacyManager.ExecuteDiplomacyMission(success, person, actionType, receiverForce, resourceValue);

                                        // 完成任务，返回原城市
                                        person.SetMission(MissionType.PersonReturn, person.mBelongCity);
                                        IsDone = true;
                                    });
                                });
                                RenderEvent.Instance.AddFront(delay);
                            }
                        }
                        break;
                    case DiplomacyActionType.Truce:
                        // 关系值越高，成功率越高，最高80%
                        break;
                    case DiplomacyActionType.DeclareWar:
                        // 宣战总是成功
                        break;
                    case DiplomacyActionType.SendGift:
                        {
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
                                    text = $"我代表我家主公{person.mBelongForce.mGovernor.ColorName}向您呈上最真挚的情谊,此次携带1000金,还请{receiverForce.mGovernor.ColorName}笑纳!",
                                },
                                 new GameDialog.TalkData
                                {
                                     person = receiverForce.mGovernor,
                                    text = $"既然{person.mBelongForce.mGovernor.ColorName}这么有心意,那我就收下了!!!",
                                },
                                };
                                GameDialog.StartTalk(talkDatas, () =>
                                {
                                    GameSystem.GetSystem<DiplomacyManager>().ExecuteDiplomacyMission(true, person, actionType, receiverForce, resourceValue);
                                    // 完成任务，返回原城市
                                    person.SetMission(MissionType.PersonReturn, person.mBelongCity);
                                    IsDone = true;
                                });
                            });
                            RenderEvent.Instance.AddFront(delay);
                            // 送礼总是成功
                        }
                        break;
                    case DiplomacyActionType.RequestTechnique:
                        // 关系值越高，成功率越高，最高85%
                        break;
                    case DiplomacyActionType.RequestTroops:
                        // 关系值越高，成功率越高，最高80%
                        break;
                    case DiplomacyActionType.Trade:
                        // 关系值越高，成功率越高，最高95%
                        break;
                    case DiplomacyActionType.Marriage:
                        // 关系值越高，成功率越高，最高95%
                        break;
                    case DiplomacyActionType.AllianceRequest:
                        // 关系值越高，成功率越高，最高85%
                        break;
                    case DiplomacyActionType.TruceRequest:
                        // 关系值越高，成功率越高，最高75%
                        break;
                    case DiplomacyActionType.Ransom:
                        // 关系值越高，成功率越高，最高90%
                        break;
                    default:
                        break;
                }

                //if (receiverForce.IsPlayer)
                //{
                //    // 计算成功率
                //    int successRate = GameSystem.GetSystem<DiplomacyManager>().CalculateDiplomacySuccessRate(actionType, person.BelongForce, receiverForce, person, resourceValue);
                //    success = GameRandom.Chance(successRate);


                //}
                //else
                //{
                //    // 计算成功率
                //    int successRate = GameSystem.GetSystem<DiplomacyManager>().CalculateDiplomacySuccessRate(actionType, person.BelongForce, receiverForce, person, resourceValue);
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
                //                    text = $"我代表我家主公{person.mBelongForce.Governor.ColorName}向您呈上最真挚的情谊,此次携带1000金,还请{person.ColorName}笑纳!",
                //                },
                //                 new GameDialog.TalkData
                //                {
                //                     person = receiverForce.Governor,
                //                    text = $"既然{person.mBelongForce.Governor.ColorName}这么有心意,那我就收下了!!!",
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
                person.SetMission(MissionType.PersonReturn, person.mBelongCity);
                IsDone = true;
            }
        }
    }
}
