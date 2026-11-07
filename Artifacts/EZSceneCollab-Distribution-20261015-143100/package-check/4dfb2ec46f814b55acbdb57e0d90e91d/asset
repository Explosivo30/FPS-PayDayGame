using System;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEngine;

namespace EZCollabTool
{
    public interface IEZCollabProBuilderIntegration
    {
        bool TryFinalizeObject(GameObject gameObject);
    }

    public static class EZCollabIntegrations
    {
        const string ProBuilderEnabledPreference = "EZCollabTool.Integrations.ProBuilder.Enabled";
        const string ProBuilderPackageName = "com.unity.probuilder";

        static IEZCollabProBuilderIntegration proBuilderIntegration;
        static string detectedProBuilderVersion;
        static bool packageDetectionComplete;

        public static event Action changed;

        public static bool proBuilderInstalled
        {
            get
            {
                DetectProBuilderPackage();
                return !string.IsNullOrEmpty(detectedProBuilderVersion);
            }
        }

        public static bool proBuilderCompatible => proBuilderIntegration != null;

        public static string proBuilderVersion
        {
            get
            {
                DetectProBuilderPackage();
                return detectedProBuilderVersion;
            }
        }

        public static bool proBuilderEnabled
        {
            get => proBuilderCompatible && EditorPrefs.GetBool(ProBuilderEnabledPreference, true);
            set
            {
                bool previous = EditorPrefs.GetBool(ProBuilderEnabledPreference, true);
                if (previous == value)
                    return;

                EditorPrefs.SetBool(ProBuilderEnabledPreference, value);
                changed?.Invoke();
            }
        }

        public static void RegisterProBuilder(IEZCollabProBuilderIntegration integration)
        {
            if (integration == null)
                throw new ArgumentNullException(nameof(integration));

            proBuilderIntegration = integration;
            packageDetectionComplete = false;
            changed?.Invoke();
        }

        public static void UnregisterProBuilder(IEZCollabProBuilderIntegration integration)
        {
            if (!ReferenceEquals(proBuilderIntegration, integration))
                return;

            proBuilderIntegration = null;
            changed?.Invoke();
        }

        public static bool TryFinalizeObject(GameObject gameObject)
        {
            if (!proBuilderEnabled || gameObject == null)
                return false;

            return proBuilderIntegration.TryFinalizeObject(gameObject);
        }

        static void DetectProBuilderPackage()
        {
            if (packageDetectionComplete)
                return;

            detectedProBuilderVersion = null;
            PackageInfo[] packages = PackageInfo.GetAllRegisteredPackages();
            if (packages != null)
            {
                foreach (PackageInfo package in packages)
                {
                    if (package != null && package.name == ProBuilderPackageName)
                    {
                        detectedProBuilderVersion = package.version;
                        break;
                    }
                }
            }

            packageDetectionComplete = true;
        }
    }
}
