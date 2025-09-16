using Newtonsoft.Json.Converters;
using System;

namespace Sango.Game
{
    public abstract class JsonConverter<T> : CustomCreationConverter<T> where T : new()
    {
        public override T Create(Type objectType)
        {
            return new T();
        }
        public override bool CanRead { get { return true; } }
        public override bool CanWrite { get { return true; } }
    }
}
