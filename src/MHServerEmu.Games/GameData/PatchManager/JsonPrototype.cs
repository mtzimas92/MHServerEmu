using MHServerEmu.Core.Logging;
using MHServerEmu.Games.GameData.Calligraphy;
using MHServerEmu.Games.GameData.Prototypes;
using System.Text.Json;

namespace MHServerEmu.Games.GameData.PatchManager
{
    public class JsonPrototype : ValueBase
    {
        private static readonly Logger Logger = LogManager.CreateLogger();

        private readonly PrototypeId _parentRef;
        private readonly List<Field> _fields = new();

        private Prototype _instance;

        public override ValueType ValueType { get => ValueType.Prototype; }

        public JsonPrototype(JsonElement jsonElement)
        {
            _parentRef = (PrototypeId)jsonElement.GetProperty("ParentDataRef").GetUInt64();

            Type classType = GameDatabase.DataDirectory.GetPrototypeClassType(_parentRef);
            if (!Verify.IsNotNull(classType)) return;

            foreach (JsonProperty jsonProperty in jsonElement.EnumerateObject())
            {
                string fieldName = jsonProperty.Name;

                if (fieldName == "ParentDataRef")
                    continue;

                System.Reflection.PropertyInfo fieldInfo = classType.GetProperty(fieldName);
                if (!Verify.IsNotNull(fieldInfo))
                    continue;

                Type fieldType = fieldInfo.PropertyType;
                object fieldValue = ParseFieldValue(jsonProperty.Value, fieldType);

                Field field = new(fieldName, fieldValue, fieldType);
                _fields.Add(field);
            }
        }

        public override object GetValue()
        {
            if (!Verify.IsTrue(_parentRef != PrototypeId.Invalid)) return null;

            if (_instance == null)
            {
                Type classType = GameDatabase.DataDirectory.GetPrototypeClassType(_parentRef);
                if (!Verify.IsNotNull(classType)) return null;

                Prototype instance = GameDatabase.PrototypeClassManager.AllocatePrototype(classType);
                if (!Verify.IsNotNull(instance)) return null;

                CalligraphySerializer.CopyPrototypeDataRefFields(instance, _parentRef);
                instance.ParentDataRef = _parentRef;

                foreach (Field field in _fields)
                {
                    System.Reflection.PropertyInfo fieldInfo = classType.GetProperty(field.Name);
                    if (!Verify.IsNotNull(fieldInfo))
                        continue;

                    try
                    {
                        object fieldValue = ResolveFieldValue(field.Value, field.Type);
                        object convertedValue = PrototypePatchManager.ConvertValue(fieldValue, field.Type);
                        fieldInfo.SetValue(instance, convertedValue);
                    }
                    catch (Exception e)
                    {
                        Logger.Warn($"Can't convert {field.Name} in {classType.Name} - {e.Message}");
                    }
                }

                _instance = instance;
            }

            return _instance;
        }

        private static object ParseFieldValue(JsonElement jsonElement, Type fieldType)
        {
            if (fieldType.IsArray && jsonElement.ValueKind == JsonValueKind.Array)
            {
                Type elementType = fieldType.GetElementType();
                if (elementType == null)
                    throw new InvalidOperationException($"Array field {fieldType.Name} has no element type.");

                return ParseArray(jsonElement, elementType);
            }

            if (jsonElement.ValueKind == JsonValueKind.Object && IsPrototypeType(fieldType))
                return new JsonPrototype(jsonElement);

            return PatchEntryConverter.ParseJsonElement(jsonElement, fieldType);
        }

        private static object ParseArray(JsonElement jsonElement, Type elementType)
        {
            if (IsPrototypeType(elementType))
            {
                JsonPrototype[] prototypes = new JsonPrototype[jsonElement.GetArrayLength()];

                int prototypeIndex = 0;
                foreach (JsonElement element in jsonElement.EnumerateArray())
                    prototypes[prototypeIndex++] = new(element);

                return prototypes;
            }

            Array array = Array.CreateInstance(elementType, jsonElement.GetArrayLength());

            int index = 0;
            foreach (JsonElement element in jsonElement.EnumerateArray())
            {
                object value = PatchEntryConverter.ParseJsonElement(element, elementType);
                value = PrototypePatchManager.ConvertValue(value, elementType);
                array.SetValue(value, index++);
            }

            return array;
        }

        private static object ResolveFieldValue(object value, Type fieldType)
        {
            if (value is JsonPrototype prototype)
                return prototype.GetValue();

            if (value is JsonPrototype[] prototypes)
            {
                Type elementType = fieldType.GetElementType();
                if (elementType == null)
                    throw new InvalidOperationException($"Array field {fieldType.Name} has no element type.");

                Array array = Array.CreateInstance(elementType, prototypes.Length);

                for (int i = 0; i < prototypes.Length; i++)
                {
                    object elementValue = prototypes[i].GetValue();
                    elementValue = PrototypePatchManager.ConvertValue(elementValue, elementType);
                    array.SetValue(elementValue, i);
                }

                return array;
            }

            return value;
        }

        private static bool IsPrototypeType(Type type)
        {
            return type == typeof(Prototype) || type.IsSubclassOf(typeof(Prototype));
        }

        private readonly struct Field(string name, object value, Type type)
        {
            public readonly string Name = name;
            public readonly object Value = value;
            public readonly Type Type = type;
        }
    }

    public class JsonPrototypeArray : ValueBase
    {
        private readonly JsonPrototype[] _jsonPrototypes;
        private Prototype[] _instances;

        public override ValueType ValueType { get => ValueType.PrototypeArray; }

        public JsonPrototypeArray(JsonElement jsonElement)
        {
            if (jsonElement.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("Json element is not array");

            JsonElement[] jsonArray = jsonElement.EnumerateArray().ToArray();
            if (jsonArray.Length == 0)
            {
                _jsonPrototypes = [];
                _instances = [];
                return;
            }

            _jsonPrototypes = new JsonPrototype[jsonArray.Length];
            for (int i = 0; i < jsonArray.Length; i++)
                _jsonPrototypes[i] = new(jsonArray[i]);
        }

        public override object GetValue()
        {
            if (_instances == null)
            {
                Prototype[] instances = new Prototype[_jsonPrototypes.Length];
                for (int i = 0; i < _jsonPrototypes.Length; i++)
                    instances[i] = (Prototype)_jsonPrototypes[i].GetValue();
                _instances = instances;
            }

            return _instances;
        }
    }
}
