using Newtonsoft.Json.Serialization;
using System;

namespace Sango.Game.Json
{
    internal class SangoObjectReferenceResolver : IReferenceResolver
    {
        public void AddReference(object context, string reference, object value)
        {
            throw new NotImplementedException();
        }

        public string GetReference(object context, object value)
        {
            throw new NotImplementedException();
        }

        public bool IsReferenced(object context, object value)
        {
            throw new NotImplementedException();
        }

        public object ResolveReference(object context, string reference)
        {
            throw new NotImplementedException();
        }
    }
}
