#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
using System;
using System.Reflection;
using GameUp.Core;

namespace GameUp.SDK
{
    /// <summary>
    /// Forward UMP consent tới mediation adapter nếu đã cài — không hard-reference asmdef
    /// để GameUp.SDK.Runtime compile được trước khi import adapter .zip.
    /// </summary>
    internal static class AdMobMediationConsentBridge
    {
        private const string UnityAdsApiAssembly = "GoogleMobileAds.Mediation.UnityAds.Api";
        private const string UnityAdsTypeName = "GoogleMobileAds.Mediation.UnityAds.Api.UnityAds";
        private const string IronSourceApiAssembly = "GoogleMobileAds.Mediation.IronSource.Api";
        private const string IronSourceTypeName = "GoogleMobileAds.Mediation.IronSource.Api.IronSource";

        internal static void ForwardGdprConsent(bool isConsent)
        {
            InvokeStatic(
                UnityAdsApiAssembly,
                UnityAdsTypeName,
                "SetConsentMetaData",
                "gdpr.consent",
                isConsent);

            InvokeStatic(
                IronSourceApiAssembly,
                IronSourceTypeName,
                "SetMetaData",
                "do_not_sell",
                isConsent ? "false" : "true");
        }

        private static void InvokeStatic(
            string assemblyName,
            string typeFullName,
            string methodName,
            string arg0,
            object arg1)
        {
            try
            {
                var assembly = FindAssembly(assemblyName);
                if (assembly == null)
                    return;

                var type = assembly.GetType(typeFullName, throwOnError: false);
                if (type == null)
                    return;

                var method = type.GetMethod(
                    methodName,
                    BindingFlags.Public | BindingFlags.Static,
                    binder: null,
                    types: new[] { typeof(string), arg1?.GetType() ?? typeof(object) },
                    modifiers: null);

                if (method == null)
                    return;

                method.Invoke(null, new[] { arg0, arg1 });
            }
            catch (Exception ex)
            {
                GULogger.Warning("GameUp", $"AdMob mediation consent ({typeFullName}.{methodName}): {ex.Message}");
            }
        }

        private static Assembly FindAssembly(string assemblyName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (string.Equals(asm.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase))
                    return asm;
            }

            return null;
        }
    }
}
#endif
