/*
'*******************************************************************
'Tank Framework
'*******************************************************************
*/
using UnityEngine;
namespace Sango
{
    public class ScriptConfig : ScriptableObject
    {
        public const string ScriptConfigName = "ScriptConfig";
        public virtual ScriptsLoaderBase GetLoader()
        {
            return null;
        }
        public static ScriptsLoaderBase Instance
        {
            get
            {
                ScriptConfig c = Resources.Load<ScriptConfig>(ScriptConfigName);
                return c ? c.GetLoader() : new ScriptsLoaderBase();
            }
        }
    }
}