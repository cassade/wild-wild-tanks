using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tanks.Core
{
    /// <summary>
    /// Вспомогательный класс для сериализации/десериализации состояния игры.
    /// Без него JSON.NET выбрасывает <see cref="FileLoadException"/> при десериализации полиморфной коллекции.
    /// </summary>
    internal class TypeNameSerializationBinder : SerializationBinder
    {
        public override void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            assemblyName = null;
            typeName = serializedType.AssemblyQualifiedName;
        }

        public override Type BindToType(string assemblyName, string typeName)
        {
            return Type.GetType(typeName, true);
        }
    }
}
