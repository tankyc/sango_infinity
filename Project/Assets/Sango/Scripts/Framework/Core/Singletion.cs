/*
'*******************************************************************
'Tank Framework
'*******************************************************************
*/

namespace Sango
{
    public abstract class Singletion<T> where T : class, new()
    {
        private static T _instance;
        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
#if UNITY_EDITOR
                    try
                    {
                        _instance = new T();
                    }
                    catch (System.Exception e)
                    {
                        UnityEngine.Debug.LogError(e);
                    }
#else
                    _instance = new T();
#endif
                }
                return _instance;
            }
        }
    }

}
