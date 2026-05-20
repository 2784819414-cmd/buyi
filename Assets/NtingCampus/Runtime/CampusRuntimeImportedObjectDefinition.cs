using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace NtingCampusMapEditor
{
    internal static class CampusRuntimeImportedObjectLibrary
    {
        internal static List<RuntimeImportedObjectDefinition> BuildDefinitions(string[] files, string importRoot)
        {
            Dictionary<string, RuntimeImportedObjectDefinition> definitionMap =
                new Dictionary<string, RuntimeImportedObjectDefinition>(StringComparer.OrdinalIgnoreCase);
            List<RuntimeImportedObjectDefinition> definitions = new List<RuntimeImportedObjectDefinition>();
            if (files == null)
            {
                return definitions;
            }

            for (int i = 0; i < files.Length; i++)
            {
                string filePath = files[i];
                string objectName = Path.GetFileNameWithoutExtension(filePath);
                if (string.IsNullOrWhiteSpace(objectName))
                {
                    continue;
                }

                if (TryParseDirectionalObjectName(objectName, out string groupedObjectName, out int rotation90))
                {
                    RuntimeImportedObjectDefinition definition =
                        GetOrCreateDefinition(definitionMap, definitions, groupedObjectName);
                    definition.DirectionSpritePaths[rotation90] =
                        CampusRuntimeImportLibrary.NormalizeSerializedPath(filePath, importRoot);
                    if (string.IsNullOrWhiteSpace(definition.BaseSpritePath) || rotation90 == 0)
                    {
                        definition.BaseSpritePath = filePath;
                    }

                    continue;
                }

                RuntimeImportedObjectDefinition standalone =
                    GetOrCreateDefinition(definitionMap, definitions, objectName);
                standalone.BaseSpritePath = filePath;
            }

            definitions.Sort((a, b) => string.Compare(a.ObjectName, b.ObjectName, StringComparison.OrdinalIgnoreCase));
            return definitions;
        }

        internal static bool TryParseDirectionalObjectName(string objectName, out string baseObjectName, out int rotation90)
        {
            baseObjectName = objectName;
            rotation90 = 0;
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return false;
            }

            Match match = Regex.Match(
                objectName,
                @"^(?<base>.+?)(?:[_\-\s]+)(?<dir>0|90|180|270|front|right|back|left|up|down|north|east|south|west|qian|hou|zuo|you|shang|xia|bei|dong|nan|xi|前|后|後|左|右|上|下|北|东|東|南|西)$",
                RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return false;
            }

            string rawBaseName = match.Groups["base"].Value.Trim();
            if (string.IsNullOrWhiteSpace(rawBaseName))
            {
                return false;
            }

            if (!TryResolveDirection(match.Groups["dir"].Value, out rotation90))
            {
                return false;
            }

            baseObjectName = rawBaseName;
            return true;
        }

        private static RuntimeImportedObjectDefinition GetOrCreateDefinition(
            Dictionary<string, RuntimeImportedObjectDefinition> definitionMap,
            List<RuntimeImportedObjectDefinition> definitions,
            string objectName)
        {
            if (definitionMap.TryGetValue(objectName, out RuntimeImportedObjectDefinition definition))
            {
                return definition;
            }

            definition = new RuntimeImportedObjectDefinition(objectName);
            definitionMap.Add(objectName, definition);
            definitions.Add(definition);
            return definition;
        }

        private static bool TryResolveDirection(string rawDirection, out int rotation90)
        {
            rotation90 = 0;
            string direction = string.IsNullOrWhiteSpace(rawDirection)
                ? string.Empty
                : rawDirection.Trim().ToLowerInvariant();
            switch (direction)
            {
                case "0":
                case "front":
                case "up":
                case "north":
                case "qian":
                case "shang":
                case "bei":
                case "前":
                case "上":
                case "北":
                    rotation90 = 0;
                    return true;
                case "90":
                case "right":
                case "east":
                case "you":
                case "dong":
                case "右":
                case "东":
                case "東":
                    rotation90 = 1;
                    return true;
                case "180":
                case "back":
                case "down":
                case "south":
                case "hou":
                case "xia":
                case "nan":
                case "后":
                case "後":
                case "下":
                case "南":
                    rotation90 = 2;
                    return true;
                case "270":
                case "left":
                case "west":
                case "zuo":
                case "xi":
                case "左":
                case "西":
                    rotation90 = 3;
                    return true;
                default:
                    return false;
            }
        }
    }

    internal sealed class RuntimeImportedObjectDefinition
    {
        public RuntimeImportedObjectDefinition(string objectName)
        {
            ObjectName = objectName;
        }

        public string ObjectName;
        public string BaseSpritePath;
        public readonly string[] DirectionSpritePaths = new string[4];

        public bool HasDirectionalSprites
        {
            get
            {
                for (int i = 0; i < DirectionSpritePaths.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(DirectionSpritePaths[i]))
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
