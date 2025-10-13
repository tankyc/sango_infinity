using System.Collections;

namespace Sango
{
    public class EventParams<T1>{public T1 v1; }
    public class EventParams<T1, T2>{public T1 v1; public T2 v2; }
    public class EventParams<T1, T2, T3> { public T1 v1; public T2 v2; public T3 v3; }
    public class EventParams<T1, T2, T3, T4> { public T1 v1; public T2 v2; public T3 v3; public T4 v4; }
    public class EventParams<T1, T2, T3, T4, T5> { public T1 v1; public T2 v2; public T3 v3; public T4 v4; public T5 v5; }
    public class EventParams<T1, T2, T3, T4, T5, T6> { public T1 v1; public T2 v2; public T3 v3; public T4 v4; public T5 v5; public T6 v6; }
    public class EventParams<T1, T2, T3, T4, T5, T6, T7> { public T1 v1; public T2 v2; public T3 v3; public T4 v4; public T5 v5; public T6 v6; public T7 v7; }

    public abstract class EventBase
    {
        public delegate void EventDelegate();
        public delegate void EventDelegate<T>(T t);
        public delegate void EventDelegate<T, T1>(T t, T1 t1);
        public delegate void EventDelegate<T, T1, T2>(T t, T1 t1, T2 t2);
        public delegate void EventDelegate<T, T1, T2, T3>(T t, T1 t1, T2 t2, T3 t3);
        public delegate void EventDelegate<T, T1, T2, T3, T4>(T t, T1 t1, T2 t2, T3 t3, T4 t4);
        public delegate void EventDelegate<T, T1, T2, T3, T4, T5>(T t, T1 t1, T2 t2, T3 t3, T4 t4, T5 t5);
        public delegate void EventDelegate<T, T1, T2, T3, T4, T5, T6>(T t, T1 t1, T2 t2, T3 t3, T4 t4, T5 t5, T6 t6);

        public delegate IEnumerator CoEventDelegate();
        public delegate IEnumerator CoEventDelegate<T>(T t);
        public delegate IEnumerator CoEventDelegate<T, T1>(T t, T1 t1);
        public delegate IEnumerator CoEventDelegate<T, T1, T2>(T t, T1 t1, T2 t2);
        public delegate IEnumerator CoEventDelegate<T, T1, T2, T3>(T t, T1 t1, T2 t2, T3 t3);
        public delegate IEnumerator CoEventDelegate<T, T1, T2, T3, T4>(T t, T1 t1, T2 t2, T3 t3, T4 t4);

        public delegate void EventDelegateParams();
        public delegate void EventDelegateParams<T1>(EventParams<T1> t);
        public delegate void EventDelegateParams<T1, T2>(EventParams<T1, T2> t);
        public delegate void EventDelegateParams<T1, T2, T3>(EventParams<T1, T2, T3> t);
        public delegate void EventDelegateParams<T1, T2, T3, T4>(EventParams<T1, T2, T3, T4> t);
        public delegate void EventDelegateParams<T1, T2, T3, T4, T5>(EventParams<T1, T2, T3, T4, T5> t);
        public delegate void EventDelegateParams<T1, T2, T3, T4, T5, T6>(EventParams<T1, T2, T3, T4, T5, T6> t);
        public delegate void EventDelegateParams<T1, T2, T3, T4, T5, T6, T7>(EventParams<T1, T2, T3, T4, T5, T6, T7> t);

        public delegate IEnumerator CoEventDelegateParams();
        public delegate IEnumerator CoEventDelegateParams<T1>(EventParams<T1> t);
        public delegate IEnumerator CoEventDelegateParams<T1, T2>(EventParams<T1, T2> t);
        public delegate IEnumerator CoEventDelegateParams<T1, T2, T3>(EventParams<T1, T2, T3> t);
        public delegate IEnumerator CoEventDelegateParams<T1, T2, T3, T4>(EventParams<T1, T2, T3, T4> t);
        public delegate IEnumerator CoEventDelegateParams<T1, T2, T3, T4, T5>(EventParams<T1, T2, T3, T4, T5> t);
        public delegate IEnumerator CoEventDelegateParams<T1, T2, T3, T4, T5, T6>(EventParams<T1, T2, T3, T4, T5, T6> t);
        public delegate IEnumerator CoEventDelegateParams<T1, T2, T3, T4, T5, T6, T7>(EventParams<T1, T2, T3, T4, T5, T6, T7> t);

        /// <summary>
        /// 游戏初始化开始
        /// </summary>
        public static EventDelegate OnGameInit;
        public static EventDelegate OnGamePause;
        public static EventDelegate OnGameResume;
        public static EventDelegate OnGameShutdown;

    }
}
