/*
'*******************************************************************
'Tank Framework
'*******************************************************************
*/

namespace Sango
{
    public abstract class System<T> : Module where T : Module, new()
    {
        private static T _instance;
        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new T();
                }
                return _instance;
            }
        }
    }

}
