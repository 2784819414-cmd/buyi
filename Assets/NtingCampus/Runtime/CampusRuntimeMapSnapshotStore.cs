using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace NtingCampusMapEditor
{
    internal static class CampusRuntimeMapSnapshotStore
    {
        internal const string Schema = "NtingCampusRuntimeMapEditor.v1";
        internal const string PlayerSaveFolder = "CampusPlayerMapSave";
        internal const string PlayerSaveMapFile = "CampusMap_PlayerSave.json";
        internal const string PlayerSaveManifestFile = "save_manifest.json";
        internal const string AuthoringPackageFolder = "UserGeneratedRuntimeContent";
        internal const string AuthoringPackageMapFile = "CampusMap_AuthoringPackage.json";
        internal const string AuthoringPackageManifestFile = "authoring_manifest.json";

        internal static string ToJson(CampusRuntimeMapSnapshot snapshot, bool prettyPrint)
        {
            return JsonUtility.ToJson(snapshot, prettyPrint);
        }

        internal static CampusRuntimeMapSnapshot FromJson(string json)
        {
            return string.IsNullOrWhiteSpace(json)
                ? null
                : JsonUtility.FromJson<CampusRuntimeMapSnapshot>(json);
        }

        internal static string GetExportFolder()
        {
            return Path.Combine(Application.persistentDataPath, "CampusMapExports");
        }

        internal static string GetPlayerSaveRootFolder()
        {
            return Path.Combine(Application.persistentDataPath, PlayerSaveFolder);
        }

        internal static string GetPlayerSaveMapPath()
        {
            return Path.Combine(GetPlayerSaveRootFolder(), PlayerSaveMapFile);
        }

        internal static string GetPlayerSaveManifestPath()
        {
            return Path.Combine(GetPlayerSaveRootFolder(), PlayerSaveManifestFile);
        }

        internal static string GetAuthoringPackageRootFolder()
        {
            return Path.Combine(Application.dataPath, "NtingCampus", AuthoringPackageFolder);
        }

        internal static string GetAuthoringPackageImportFolder()
        {
            return Path.Combine(GetAuthoringPackageRootFolder(), CampusRuntimeImportLibrary.RuntimeImportFolder);
        }

        internal static string GetAuthoringPackageMapPath()
        {
            return Path.Combine(GetAuthoringPackageRootFolder(), AuthoringPackageMapFile);
        }

        internal static string GetAuthoringPackageManifestPath()
        {
            return Path.Combine(GetAuthoringPackageRootFolder(), AuthoringPackageManifestFile);
        }

        internal static string WriteMap(string path, CampusRuntimeMapSnapshot snapshot, bool prettyPrint)
        {
            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllText(path, ToJson(snapshot, prettyPrint), Encoding.UTF8);
            return path;
        }

        internal static CampusRuntimeMapSnapshot ReadMap(string path)
        {
            return FromJson(File.ReadAllText(path, Encoding.UTF8));
        }

        internal static string ExportMap(CampusRuntimeMapSnapshot snapshot)
        {
            string folder = GetExportFolder();
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "CampusMap_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json");
            return WriteMap(path, snapshot, true);
        }

        internal static string WritePlayerSave(CampusRuntimeMapSnapshot snapshot)
        {
            string saveRoot = GetPlayerSaveRootFolder();
            Directory.CreateDirectory(saveRoot);
            string mapPath = WriteMap(GetPlayerSaveMapPath(), snapshot, true);
            CampusRuntimePlayerMapSaveManifest manifest = new CampusRuntimePlayerMapSaveManifest
            {
                SavedAtLocal = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                UnityPersistentDataPath = string.Empty,
                ImportRootFolderName = CampusRuntimeImportLibrary.RuntimeImportFolder,
                MapFileName = PlayerSaveMapFile
            };
            File.WriteAllText(GetPlayerSaveManifestPath(), JsonUtility.ToJson(manifest, true), Encoding.UTF8);
            return mapPath;
        }

        internal static string WriteAuthoringPackage(CampusRuntimeMapSnapshot snapshot, string sourceImportRoot)
        {
            string packageRoot = GetAuthoringPackageRootFolder();
            string packageImportFolder = GetAuthoringPackageImportFolder();
            Directory.CreateDirectory(packageRoot);
            if (!CampusRuntimeImportLibrary.AreSamePath(sourceImportRoot, packageImportFolder))
            {
                CampusRuntimeImportLibrary.MirrorDirectory(sourceImportRoot, packageImportFolder, true);
            }

            string mapPath = WriteMap(GetAuthoringPackageMapPath(), snapshot, true);
            CampusRuntimeAuthoringPackageManifest manifest = new CampusRuntimeAuthoringPackageManifest
            {
                ExportedAtLocal = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                UnityPersistentDataPath = string.Empty,
                ImportRootFolderName = CampusRuntimeImportLibrary.RuntimeImportFolder,
                MapFileName = AuthoringPackageMapFile
            };
            File.WriteAllText(GetAuthoringPackageManifestPath(), JsonUtility.ToJson(manifest, true), Encoding.UTF8);
            return mapPath;
        }
    }
}
