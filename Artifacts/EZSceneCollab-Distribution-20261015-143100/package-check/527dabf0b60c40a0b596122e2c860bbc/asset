using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EZCollabTool
{
    public static class EZCollabAssetValidator
    {
        static readonly Dictionary<string, string> dependencyHashes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static void ResetCache()
        {
            dependencyHashes.Clear();
        }

        public static void DescribeAsset(string path, out string guid, out string dependencyHash)
        {
            guid = string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path);
            dependencyHash = GetDependencyHash(path);
        }

        public static string GetDependencyHash(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            if (dependencyHashes.TryGetValue(path, out string cached))
                return cached;

            string hash;
            try
            {
                hash = AssetDatabase.GetAssetDependencyHash(path).ToString();
            }
            catch
            {
                hash = null;
            }

            dependencyHashes[path] = hash;
            return hash;
        }

        public static string ResolveAndValidate(
            string expectedPath,
            string expectedGuid,
            string expectedHash,
            string context)
        {
            string localPath = !string.IsNullOrEmpty(expectedGuid)
                ? AssetDatabase.GUIDToAssetPath(expectedGuid)
                : expectedPath;

            if (string.IsNullOrEmpty(localPath) && !string.IsNullOrEmpty(expectedPath) &&
                AssetDatabase.LoadMainAssetAtPath(expectedPath) != null)
            {
                localPath = expectedPath;
                if (!string.IsNullOrEmpty(expectedGuid))
                {
                    EZCollabState.ReportAssetWarning(EZCollabLocalization.Tr(
                        EZCollabText.AssetGuidMismatch,
                        expectedPath,
                        context));
                }
            }

            if (string.IsNullOrEmpty(localPath))
            {
                EZCollabState.ReportAssetWarning(EZCollabLocalization.Tr(
                    EZCollabText.AssetMissing,
                    expectedPath,
                    context));
                return null;
            }

            if (!string.IsNullOrEmpty(expectedPath) &&
                !string.Equals(localPath, expectedPath, StringComparison.OrdinalIgnoreCase))
            {
                EZCollabState.ReportAssetWarning(EZCollabLocalization.Tr(
                    EZCollabText.AssetPathMismatch,
                    context,
                    expectedPath,
                    localPath));
            }

            if (!string.IsNullOrEmpty(expectedHash))
            {
                string localHash = GetDependencyHash(localPath);
                if (!string.Equals(localHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    EZCollabState.ReportAssetWarning(EZCollabLocalization.Tr(
                        EZCollabText.AssetHashMismatch,
                        localPath,
                        context));
                }
            }

            return localPath;
        }
    }
}
