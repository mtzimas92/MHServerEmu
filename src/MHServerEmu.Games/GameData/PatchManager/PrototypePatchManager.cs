using MHServerEmu.Core.Helpers;
using MHServerEmu.Core.Logging;
using MHServerEmu.Core.VectorMath;
using MHServerEmu.Games.GameData.Prototypes;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace MHServerEmu.Games.GameData.PatchManager
{
    public class PrototypePatchManager
    {
        private static readonly Logger Logger = LogManager.CreateLogger();
        
        private readonly Dictionary<PrototypeId, List<PrototypePatchEntry>> _patchDict = new();
        private PatchContext _context = new();
        private bool _initialized = false;

        public static PrototypePatchManager Instance { get; } = new();

        /// <summary>
        /// Loads prototype patches.
        /// </summary>
        public void Initialize(bool enablePatchManager)
        {
            if (enablePatchManager) _initialized |= LoadPatchDataFromDisk("PatchData");
        }
        
        private bool LoadPatchDataFromDisk(string prefix)
        {
            string patchDirectory = Path.Combine(FileHelper.DataDirectory, "Game", "Patches");
            if (Directory.Exists(patchDirectory) == false)
                return Logger.WarnReturn(false, "LoadPatchDataFromDisk(): Game data directory not found");

            int count = 0;
            var options = new JsonSerializerOptions { Converters = { new PatchEntryConverter() } };

            // Read all .json files that start with the specified prefix
            foreach (string filePath in FileHelper.GetFilesWithPrefix(patchDirectory, prefix, "json"))
            {
                string fileName = Path.GetFileName(filePath);

                PrototypePatchEntry[] updateValues = FileHelper.DeserializeJson<PrototypePatchEntry[]>(filePath, options);
                if (updateValues == null)
                {
                    Logger.Warn($"LoadPatchDataFromDisk(): Failed to parse {fileName}, skipping");
                    continue;
                }

                foreach (PrototypePatchEntry value in updateValues)
                {
                    if (value.Enabled == false) continue;
                    PrototypeId prototypeId = GameDatabase.GetPrototypeRefByName(value.Prototype);
                    if (prototypeId == PrototypeId.Invalid) continue;
                    value.SourceFile = fileName;
                    AddPatchValue(prototypeId, value);
                    count++;
                }

                Logger.Trace($"Parsed patch data from {fileName}");
            }

            if (count == 0)
                return false;

            Logger.Info($"Loaded {count} {prefix} patches");
            return true;
        }

        private void AddPatchValue(PrototypeId prototypeId, in PrototypePatchEntry value)
        {
            if (_patchDict.TryGetValue(prototypeId, out var patchList) == false)
            {
                patchList = [];
                _patchDict[prototypeId] = patchList;
            }
            patchList.Add(value);
        }

        public bool CheckProperties(PrototypeId protoRef, out Properties.PropertyCollection prop)
        {
            prop = null;
            if (_initialized == false) return false;

            if (protoRef != PrototypeId.Invalid && _patchDict.TryGetValue(protoRef, out var list))
                foreach (var entry in list)
                    if (entry.Value.ValueType == ValueType.Properties)
                    {
                        prop = entry.Value.GetValue() as Properties.PropertyCollection;
                        return prop != null;
                    }

            return false;
        }

        public bool PreCheck(PrototypeId protoRef)
        {
            if (_initialized == false) return false;

            if (protoRef != PrototypeId.Invalid && _patchDict.TryGetValue(protoRef, out var list))
            {
                if (NotPatched(list))
                    _context.ProtoStack.Push(protoRef);
            }

            return _context.ProtoStack.Count > 0;
        }

        private static bool NotPatched(List<PrototypePatchEntry> list)
        {
            foreach (var entry in list)
                if (entry.Patched == false) return true;
            return false;
        }

        public void PostOverride(Prototype prototype)
        {
            if (_context.ProtoStack.Count == 0) return;

            string currentPath = string.Empty;
            if (prototype.DataRef == PrototypeId.Invalid 
                && _context.PathDict.TryGetValue(prototype, out currentPath) == false) return;

            PrototypeId patchProtoRef = _context.ProtoStack.Peek();
            if (prototype.DataRef != PrototypeId.Invalid)
            {
                if (prototype.DataRef != patchProtoRef) return;
                if (_patchDict.ContainsKey(prototype.DataRef))
                    patchProtoRef = _context.ProtoStack.Pop();
            }

            if (_patchDict.TryGetValue(patchProtoRef, out var list) == false) return;

            foreach (var entry in list)
                if (entry.Patched == false)
                    CheckAndUpdate(entry, prototype, currentPath);

            if (_context.ProtoStack.Count == 0)
                _context.PathDict.Clear();
        }

        private static bool CheckAndUpdate(PrototypePatchEntry entry, Prototype prototype, string currentPath)
        {
            if (currentPath.StartsWith('.')) currentPath = currentPath[1..];

            if (IsPropertyIdParamPath(entry, currentPath, out string propertyFieldName, out int paramIndex))
            {
                entry.MatchAttempts++;

                if (UpdatePropertyIdParam(prototype, propertyFieldName, paramIndex, entry, out string propertyError) == false)
                {
                    entry.LastError = propertyError;
                    Logger.Warn($"CheckAndUpdate: {entry.LastError} for {entry.Prototype}");
                    return false;
                }

                entry.Patched = true;
                entry.LastError = string.Empty;
                Logger.Trace($"Patch Prototype: {entry.Prototype} {entry.Path} = {entry.Value.GetValue()}");

                return true;
            }

            if (entry.СlearPath != currentPath) return false;
            entry.MatchAttempts++;

            var fieldInfo = prototype.GetType().GetProperty(entry.FieldName);
            if (fieldInfo == null)
            {
                entry.LastError = $"{entry.FieldName} not found at {currentPath}";
                Logger.Warn($"CheckAndUpdate: {entry.LastError} for {entry.Prototype}");
                return false;
            }

            if (UpdateValue(prototype, fieldInfo, entry, out string error) == false)
            {
                entry.LastError = error;
                return false;
            }

            entry.LastError = string.Empty;
            Logger.Trace($"Patch Prototype: {entry.Prototype} {entry.Path} = {entry.Value.GetValue()}");

            return true;
        }

        private static bool IsPropertyIdParamPath(PrototypePatchEntry entry, string currentPath, out string propertyFieldName, out int paramIndex)
        {
            propertyFieldName = string.Empty;
            paramIndex = -1;

            if (entry.FieldName.StartsWith("Param", StringComparison.OrdinalIgnoreCase) == false ||
                int.TryParse(entry.FieldName[5..], out paramIndex) == false)
            {
                return false;
            }

            int lastDotIndex = entry.СlearPath.LastIndexOf('.');
            string parentPath;
            if (lastDotIndex == -1)
            {
                parentPath = string.Empty;
                propertyFieldName = entry.СlearPath;
            }
            else
            {
                parentPath = entry.СlearPath[..lastDotIndex];
                propertyFieldName = entry.СlearPath[(lastDotIndex + 1)..];
            }

            return parentPath == currentPath;
        }

        private static bool UpdatePropertyIdParam(Prototype prototype, string propertyFieldName, int paramIndex, PrototypePatchEntry entry, out string error)
        {
            error = string.Empty;

            System.Reflection.PropertyInfo propertyFieldInfo = prototype.GetType().GetProperty(propertyFieldName);
            if (propertyFieldInfo == null)
            {
                error = $"{propertyFieldName} not found for virtual PropertyId param path";
                return false;
            }

            if (propertyFieldInfo.PropertyType != typeof(MHServerEmu.Games.Properties.PropertyId))
            {
                error = $"{propertyFieldName} is {propertyFieldInfo.PropertyType.Name}, not PropertyId";
                return false;
            }

            var propertyId = (MHServerEmu.Games.Properties.PropertyId)propertyFieldInfo.GetValue(prototype);
            var propertyInfo = GameDatabase.PropertyInfoTable.LookupPropertyInfo(propertyId.Enum);
            if (paramIndex < 0 || paramIndex >= propertyInfo.ParamCount)
            {
                error = $"{propertyFieldName}.{entry.FieldName} is invalid for {propertyId}; paramCount={propertyInfo.ParamCount}";
                return false;
            }

            Span<MHServerEmu.Games.Properties.PropertyParam> paramValues = stackalloc MHServerEmu.Games.Properties.PropertyParam[MHServerEmu.Games.Properties.Property.MaxParamCount];
            propertyId.GetParams(paramValues);
            paramValues[paramIndex] = ConvertValueToPropertyParam(entry.Value.GetValue(), propertyId.Enum, propertyInfo, paramIndex);

            var updatedPropertyId = new MHServerEmu.Games.Properties.PropertyId(propertyId.Enum, paramValues);
            propertyFieldInfo.SetValue(prototype, updatedPropertyId);

            return true;
        }

        private static MHServerEmu.Games.Properties.PropertyParam ConvertValueToPropertyParam(object rawValue,
            MHServerEmu.Games.Properties.PropertyEnum propertyEnum,
            MHServerEmu.Games.Properties.PropertyInfo propertyInfo,
            int paramIndex)
        {
            switch (propertyInfo.GetParamType(paramIndex))
            {
                case MHServerEmu.Games.Properties.PropertyParamType.Asset:
                    MHServerEmu.Games.GameData.AssetId assetId = rawValue switch
                    {
                        MHServerEmu.Games.GameData.AssetId value => value,
                        ulong value => (MHServerEmu.Games.GameData.AssetId)value,
                        long value when value >= 0 => (MHServerEmu.Games.GameData.AssetId)(ulong)value,
                        _ => throw new InvalidOperationException($"Cannot convert {rawValue.GetType().Name} to AssetId for PropertyId parameter.")
                    };
                    return MHServerEmu.Games.Properties.Property.ToParam(assetId);

                case MHServerEmu.Games.Properties.PropertyParamType.Prototype:
                    PrototypeId prototypeId = rawValue switch
                    {
                        PrototypeId value => value,
                        ulong value => (PrototypeId)value,
                        long value when value >= 0 => (PrototypeId)(ulong)value,
                        string value => GameDatabase.GetPrototypeRefByName(value),
                        _ => throw new InvalidOperationException($"Cannot convert {rawValue.GetType().Name} to PrototypeId for PropertyId parameter.")
                    };
                    return MHServerEmu.Games.Properties.Property.ToParam(propertyEnum, paramIndex, prototypeId);

                case MHServerEmu.Games.Properties.PropertyParamType.Integer:
                    int intValue = rawValue switch
                    {
                        int value => value,
                        uint value => (int)value,
                        long value => (int)value,
                        ulong value => (int)value,
                        _ => Convert.ToInt32(rawValue)
                    };
                    return (MHServerEmu.Games.Properties.PropertyParam)intValue;

                default:
                    throw new InvalidOperationException($"Unsupported PropertyId parameter type {propertyInfo.GetParamType(paramIndex)}.");
            }
        }

        public string BuildPatchStatusReport(string filter = "", bool forceLoad = true, int maxEntries = 120)
        {
            if (_initialized == false)
                return "Prototype patch manager is not initialized.";

            filter ??= string.Empty;
            bool hasFilter = string.IsNullOrWhiteSpace(filter) == false;

            List<(PrototypeId ProtoRef, PrototypePatchEntry Entry)> entries = new();
            foreach ((PrototypeId protoRef, List<PrototypePatchEntry> patchList) in _patchDict)
            {
                foreach (PrototypePatchEntry entry in patchList)
                {
                    if (hasFilter &&
                        entry.Prototype.Contains(filter, StringComparison.OrdinalIgnoreCase) == false &&
                        entry.Path.Contains(filter, StringComparison.OrdinalIgnoreCase) == false &&
                        entry.Description.Contains(filter, StringComparison.OrdinalIgnoreCase) == false &&
                        entry.SourceFile.Contains(filter, StringComparison.OrdinalIgnoreCase) == false)
                    {
                        continue;
                    }

                    entries.Add((protoRef, entry));
                }
            }

            if (forceLoad)
            {
                foreach (PrototypeId protoRef in entries.Select(entry => entry.ProtoRef).Distinct())
                {
                    try
                    {
                        GameDatabase.GetPrototype<Prototype>(protoRef);
                    }
                    catch (Exception ex)
                    {
                        foreach (PrototypePatchEntry entry in entries.Where(entry => entry.ProtoRef == protoRef).Select(entry => entry.Entry))
                            entry.LastError = $"Prototype load failed: {ex.Message}";
                    }
                }
            }

            int applied = entries.Count(entry => entry.Entry.Patched);
            int failed = entries.Count(entry => IsFailed(entry.Entry, forceLoad));
            int pending = entries.Count - applied - failed;

            StringBuilder sb = new();
            sb.AppendLine($"Patch status: {applied}/{entries.Count} applied, {failed} failed/not applied, {pending} pending. ForceLoad={forceLoad} Filter=\"{filter}\"");

            foreach ((PrototypeId _, PrototypePatchEntry entry) in entries
                         .OrderBy(entry => GetPatchStateSort(entry.Entry, forceLoad))
                         .ThenBy(entry => entry.Entry.SourceFile, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(entry => entry.Entry.Prototype, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(entry => entry.Entry.Path, StringComparer.OrdinalIgnoreCase)
                         .Take(maxEntries))
            {
                string state = GetPatchState(entry, forceLoad);
                string file = string.IsNullOrWhiteSpace(entry.SourceFile) ? "<unknown>" : entry.SourceFile;
                sb.AppendLine($"{state}: {file} | {entry.Prototype} | {entry.Path}");

                if (string.IsNullOrWhiteSpace(entry.LastError) == false)
                    sb.AppendLine($"  {entry.LastError}");
                else if (state == "NOT_APPLIED")
                    sb.AppendLine("  Path was not reached after loading the target prototype.");
            }

            if (entries.Count > maxEntries)
                sb.AppendLine($"... {entries.Count - maxEntries} more entries omitted. Add a filter to narrow the report.");

            return sb.ToString();
        }

        public static object ConvertValue(object rawValue, Type targetType)
        {
            if (targetType.IsInstanceOfType(rawValue))
                return rawValue;

            // Handle array types - convert to correct element type if needed
            if (targetType.IsArray && rawValue.GetType().IsArray)
            {
                Type targetElementType = targetType.GetElementType();
                Type sourceElementType = rawValue.GetType().GetElementType();

                // If element types are compatible, create new array with correct type
                if (targetElementType != sourceElementType &&
                    (targetElementType.IsAssignableFrom(sourceElementType) || sourceElementType.IsAssignableFrom(targetElementType)))
                {
                    Array sourceArray = (Array)rawValue;
                    Array targetArray = Array.CreateInstance(targetElementType, sourceArray.Length);

                    for (int i = 0; i < sourceArray.Length; i++)
                    {
                        object element = sourceArray.GetValue(i);
                        if (element != null && targetElementType.IsInstanceOfType(element))
                            targetArray.SetValue(element, i);
                        else if (element != null)
                            targetArray.SetValue(ConvertValue(element, targetElementType), i);
                        else
                            targetArray.SetValue(null, i);
                    }

                    return targetArray;
                }

                return rawValue;
            }

            if (targetType == typeof(Prototype) || targetType.IsSubclassOf(typeof(Prototype)))
            {
                PrototypeId? protoRef = null;
                switch (rawValue)
                {
                    case PrototypeId protoId:   protoRef = protoId; break;
                    case ulong dataId:          protoRef = (PrototypeId)dataId; break;
                }

                if (protoRef.HasValue)
                {
                    PatchContext contextBefore = Instance.CreateSubContext();

                    Prototype proto = GameDatabase.GetPrototype<Prototype>(protoRef.Value);

                    Instance.RestoreContext(contextBefore);

                    return proto;
                }
            }

            if (targetType == typeof(Orientation) && rawValue is Vector3 vec)
                return new Orientation(vec.X, vec.Y, vec.Z);

            if (targetType == typeof(MHServerEmu.Games.Properties.PropertyId))
            {
                switch (rawValue)
                {
                    case MHServerEmu.Games.Properties.PropertyEnum propertyEnum:
                        return new MHServerEmu.Games.Properties.PropertyId(propertyEnum);
                    case string propertyName when Enum.TryParse(propertyName, out MHServerEmu.Games.Properties.PropertyEnum propertyEnum):
                        return new MHServerEmu.Games.Properties.PropertyId(propertyEnum);
                    case ulong rawPropertyId:
                        return new MHServerEmu.Games.Properties.PropertyId(rawPropertyId);
                    case long rawPropertyId when rawPropertyId >= 0:
                        return new MHServerEmu.Games.Properties.PropertyId((ulong)rawPropertyId);
                }
            }

            TypeConverter converter = TypeDescriptor.GetConverter(targetType);
            if (converter != null && converter.CanConvertFrom(rawValue.GetType()))
                return converter.ConvertFrom(rawValue);

            return Convert.ChangeType(rawValue, targetType);
        }

        private static bool UpdateValue(Prototype prototype, PropertyInfo fieldInfo, PrototypePatchEntry entry, out string error)
        {
            error = string.Empty;

            try
            {
                Type fieldType = fieldInfo.PropertyType;
                
                if (entry.ArrayValue)
                {
                    if (entry.ArrayIndex != -1)
                        SetIndexValue(prototype, fieldInfo, entry.ArrayIndex, entry.Value);
                    else
                        InsertValue(prototype, fieldInfo, entry.Value);
                }
                else
                {
                    object convertedValue = ConvertValue(entry.Value.GetValue(), fieldType);
                    fieldInfo.SetValue(prototype, convertedValue);
                }
                entry.Patched = true;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Logger.WarnException(ex, $"Failed UpdateValue: [{entry.Prototype}] [{entry.Path}] {ex.Message}");
                return false;
            }
        }

        private static bool IsFailed(PrototypePatchEntry entry, bool forceLoad)
        {
            if (entry.Patched)
                return false;

            return string.IsNullOrWhiteSpace(entry.LastError) == false || forceLoad;
        }

        private static string GetPatchState(PrototypePatchEntry entry, bool forceLoad)
        {
            if (entry.Patched)
                return "APPLIED";

            if (string.IsNullOrWhiteSpace(entry.LastError) == false)
                return "FAILED";

            return forceLoad ? "NOT_APPLIED" : "PENDING";
        }

        private static int GetPatchStateSort(PrototypePatchEntry entry, bool forceLoad)
        {
            string state = GetPatchState(entry, forceLoad);
            return state switch
            {
                "FAILED" => 0,
                "NOT_APPLIED" => 1,
                "PENDING" => 2,
                _ => 3
            };
        }

        private static void SetIndexValue(Prototype prototype, PropertyInfo fieldInfo, int index, ValueBase value)
        {
            Type fieldType = fieldInfo.PropertyType;
            if (fieldType.IsArray == false)
                throw new InvalidOperationException($"Field {fieldInfo.Name} is not array.");

            Array array = (Array)fieldInfo.GetValue(prototype);
            if (array == null || index < 0 || index >= array.Length)
                throw new IndexOutOfRangeException($"Invalid index {index} for array {fieldInfo.Name}.");

            object valueEntry = value.GetValue();

            var entryType = valueEntry.GetType();
            Type elementType = fieldType.GetElementType();

            if (elementType == null || IsTypeCompatible(elementType, entryType, value.ValueType) == false)
                throw new InvalidOperationException($"Type {value.ValueType} is not assignable to {elementType?.Name}.");

            object converted = GetElementValue(valueEntry, elementType);
            array.SetValue(converted, index);
        }

        private static void InsertValue(Prototype prototype, PropertyInfo fieldInfo, ValueBase value)
        {
            Type fieldType = fieldInfo.PropertyType; 
            if (fieldType.IsArray == false)
                throw new InvalidOperationException($"Field {fieldInfo.Name} is not array.");     

            var valueEntry = value.GetValue();

            var entryType = valueEntry.GetType();
            Type elementType = fieldType.GetElementType(); 

            if (elementType == null || IsTypeCompatible(elementType, entryType, value.ValueType) == false)
                throw new InvalidOperationException($"Type {value.ValueType} is not assignable for {elementType?.Name}.");

            var currentArray = (Array)fieldInfo.GetValue(prototype);

            int newLength = CalcNewLength(currentArray, valueEntry);
            var newArray = Array.CreateInstance(elementType, newLength);

            if (currentArray != null)
                Array.Copy(currentArray, newArray, currentArray.Length);

            AddElements(newArray, elementType, valueEntry, currentArray?.Length ?? 0);

            fieldInfo.SetValue(prototype, newArray);
        }

        private static int CalcNewLength(Array currentArray, object valueEntry)
        {
            int currentLength = currentArray?.Length ?? 0;
            int valuesCount = 1;
            if (valueEntry is Array array)
                valuesCount = array.Length;

            return currentLength + valuesCount;
        }

        private static bool IsTypeCompatible(Type baseType, Type entryType, ValueType valueType)
        {
            if (entryType.IsArray) entryType = entryType.GetElementType();
            if (valueType == ValueType.PrototypeDataRef || valueType == ValueType.PrototypeDataRefArray) 
                entryType = typeof(Prototype);
            return baseType.IsAssignableFrom(entryType) || entryType.IsAssignableFrom(baseType);
        }

        private static void AddElements(Array newArray, Type elementType, object valueEntry, int lastIndex)
        {
            if (valueEntry is Array array)
            {
                foreach (var entry in array)
                {
                    object elementValue = GetElementValue(entry, elementType);
                    newArray.SetValue(elementValue, lastIndex++);
                }
            }
            else
            {
                object elementValue = GetElementValue(valueEntry, elementType);
                newArray.SetValue(elementValue, lastIndex);
            }
        }

        private static object GetElementValue(object valueEntry, Type elementType)
        {
            if (elementType.IsClass && valueEntry is PrototypeId dataRef)
            {
                var prototype = GameDatabase.GetPrototype<Prototype>(dataRef) 
                    ?? throw new InvalidOperationException($"DataRef {dataRef} is not Prototype.");
                valueEntry = prototype;
            }

            // If valueEntry is already the correct type (e.g., Prototype), return it directly
            if (elementType.IsInstanceOfType(valueEntry))
                return valueEntry;

            return ConvertValue(valueEntry, elementType);            
        }

        public void SetPath(Prototype parent, Prototype child, string fieldName)
        {
            string parentPath = _context.PathDict.TryGetValue(parent, out var path) ? path : string.Empty;
            if (parent.DataRef != PrototypeId.Invalid && _patchDict.ContainsKey(parent.DataRef)) 
                parentPath = string.Empty;
            _context.PathDict[child] = $"{parentPath}.{fieldName}";
        }

        public void SetPathIndex(Prototype parent, Prototype child, string fieldName, int index)
        {
            string parentPath = _context.PathDict.TryGetValue(parent, out var path) ? path : string.Empty;
            if (parent.DataRef != PrototypeId.Invalid && _patchDict.ContainsKey(parent.DataRef)) 
                parentPath = string.Empty;
            _context.PathDict[child] = $"{parentPath}.{fieldName}[{index}]";
        }

        private PatchContext CreateSubContext()
        {
            PatchContext oldContext = _context;
            _context = new();
            return oldContext;
        }

        private void RestoreContext(in PatchContext context)
        {
            _context = context;
        }

        private readonly struct PatchContext
        {
            public readonly Stack<PrototypeId> ProtoStack;
            public readonly Dictionary<Prototype, string> PathDict;

            public PatchContext()
            {
                ProtoStack = new();
                PathDict = new();
            }
        }
    }
}

