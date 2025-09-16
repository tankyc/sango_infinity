using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sango.Game
{
    /// <summary>
    /// 按string数据保存
    /// </summary>
    public interface IDataString
    {
        public string ToString();
        public void FromString(string data);
    }
}
