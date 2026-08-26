//namespace Sango.Core
//{
//    /// <summary>
//    /// 移动端的一个取消按钮
//    /// </summary>
//    [GameSystem]
//    public class MobileCancelButton : GameSystem
//    {
//        public override void Init()
//        {
//            GameEvent.OnSystemStart += OnSystemStart;
//            GameEvent.OnSystemEnd += OnSystemEnd;
//        }
//        void OnSystemStart()
//        {
//#if UNITY_ANDROID || UNITY_IPHONE
//             Window.Instance.Open("window_mobile_cancel");
//#endif
//        }

//        void OnSystemEnd()
//        {
//#if UNITY_ANDROID || UNITY_IPHONE 
//             Window.Instance.Close("window_mobile_cancel");
//#endif
//        }
//    }
//}
