using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sango.Game
{
    public struct PrepareData<T>
    {
        public T linkObj;
        public int Id;
        public Action<T, int> action;

        public void Prepare()
        {
            action?.Invoke(linkObj, Id);
        }
    }
}
