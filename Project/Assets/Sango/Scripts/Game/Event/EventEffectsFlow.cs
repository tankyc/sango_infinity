/*
 * 文件名：EventEffectsFlow.cs
 * 描述：流程类事件效果——旗标、私有变量
 * 创建日期：2026-09-25
 * 最后修改：2026-09-25
 */

namespace Sango.Core.Event
{
    /// <summary>
    /// 设置事件旗标。
    ///
    /// 默认写**全局旗标**（跨事件可读、随存档持久化），因为绝大多数用法是
    /// "某情节已完成，后续事件据此判断"——例如桃园结义写 SwornBrothersDone，
    /// 其他事件用 FlagCheck 读取。
    /// 需要事件私有旗标时把 Scope 设成 "Event"。
    /// </summary>
    [EventEffectMeta("设置事件旗标", "写入一个旗标，供其他事件用 FlagCheck 条件读取", "Flag", "Value", "Scope")]
    public class SetFlagEffect : EventEffectBase
    {
        /// <summary>执行：写旗标</summary>
        /// <param name="context">事件上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            string flag = GetString("Flag");
            if (string.IsNullOrEmpty(flag))
            {
                Log.Warning("SetFlag 效果缺少 Flag 参数");
                return false;
            }

            bool value = GetBool("Value", true);
            bool isEventScope = GetString("Scope") == "Event";

            if (isEventScope)
            {
                if (context == null || context.state == null) return false;
                context.state.SetFlag(flag, value);
            }
            else
            {
                if (context == null) return false;
                context.SetGlobalFlag(flag, value);
            }

            Log.Info($"事件旗标：{flag} = {value}（{(isEventScope ? "事件私有" : "全局")}）");
            return true;
        }
    }

    /// <summary>
    /// 设置 / 增减事件私有变量。
    /// Op 支持 Set（默认）/ Add / Sub；变量可在条件与效果里用 "#名字" 引用。
    /// </summary>
    [EventEffectMeta("设置事件变量", "写入或增减本事件的私有变量，可被 #名字 引用", "Var", "Value", "Op")]
    public class SetVarEffect : EventEffectBase
    {
        /// <summary>执行：改变量</summary>
        /// <param name="context">事件上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            if (context == null) return false;

            string varName = GetString("Var");
            if (string.IsNullOrEmpty(varName))
            {
                Log.Warning("SetVar 效果缺少 Var 参数");
                return false;
            }

            int value = EvalInt("Value", context);
            string op = GetString("Op", "Set");

            int result;
            switch (op)
            {
                case "Add":
                    result = context.GetVar(varName) + value;
                    break;
                case "Sub":
                    result = context.GetVar(varName) - value;
                    break;
                default:
                    result = value;
                    break;
            }

            context.SetVar(varName, result);
            Log.Info($"事件变量：{varName} = {result}（{op} {value}）");
            return true;
        }
    }

    /// <summary>流程类效果的批量注册入口</summary>
    public static class EventEffectsFlow
    {
        /// <summary>注册本文件内的全部效果</summary>
        public static void RegisterAll()
        {
            EventEffectBase.Register("SetFlag", EventEffectBase.CreateHandle<SetFlagEffect>);
            EventEffectBase.Register("SetVar", EventEffectBase.CreateHandle<SetVarEffect>);
        }
    }
}
