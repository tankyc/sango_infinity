using System.IO;

namespace Sango.Game
{

    public abstract class ScenarioByteObject<T> where T : SangoObject, new()
    {
        protected byte _id = 0;
        protected T _obj = null;

        public int Id
        {
            get
            {
                if (_obj != null)
                {
                    return _obj.Id;
                }
                else { return _id; }
            }
            set
            {
                if (_id != value)
                {
                    _id = (byte)value;
                    _obj = null;
                }
            }
        }
        public T Object
        {
            get
            {
                if (_id > 0)
                {
                    _obj = Get(_id);
                    if (_obj == null)
                    {
                        // 报错
                    }
                    _id = 0;
                }
                return _obj;
            }
            set
            {
                _obj = value;
                _id = 0;
            }
        }
        public bool IsValid() { return Id > 0; }
        protected virtual T Get(byte id) { return null; }
      
    }
}
