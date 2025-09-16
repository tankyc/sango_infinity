using LuaInterface;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Sango;

namespace Sango
{
    public class LuaLoaderConfig : ScriptConfig
    {
        [Header("根目录")]
        public List<string> searchingPath = new List<string>();
        public override ScriptsLoaderBase GetLoader()
        {
            LuaLoader loader = new LuaLoader();
            foreach (string path in searchingPath)
                loader.AddSearchPath(string.Format("{0}/Lua/{1}/?.lua", Path.ContentRootPath, path));
            return loader;
        }
    }

}