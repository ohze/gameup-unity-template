using System;
using System.Reflection;
using GameUp.Core;

namespace GameUp.IAP
{
    /// <summary>
    /// Lấy tangle data (public key Google Play / root certificate Apple) do cửa sổ
    /// <c>Services → In-App Purchasing → Receipt Validation Obfuscator</c> sinh ra.
    /// File sinh ra nằm ở <c>Assets/Scripts/UnityPurchasing/generated</c> nên thuộc
    /// Assembly-CSharp; asmdef <c>GameUp.IAP.Runtime</c> không tham chiếu trực tiếp được,
    /// vì vậy phải đọc qua reflection.
    /// </summary>
    public static class IAPTangleProvider
    {
        private const string Tag = "IAP";
        private const string GooglePlayTangleTypeName = "UnityEngine.Purchasing.Security.GooglePlayTangle";
        private const string AppleTangleTypeName = "UnityEngine.Purchasing.Security.AppleTangle";

        public static byte[] GetGooglePlayPublicKey()
        {
            return GetTangleData(GooglePlayTangleTypeName);
        }

        public static byte[] GetAppleRootCertificate()
        {
            return GetTangleData(AppleTangleTypeName);
        }

        private static byte[] GetTangleData(string typeName)
        {
            var tangleType = FindType(typeName);
            if (tangleType == null)
            {
                GULogger.Warning(Tag,
                    $"Tangle class not found: {typeName}. Run Services > In-App Purchasing > Receipt Validation Obfuscator.");
                return null;
            }

            var dataMethod = tangleType.GetMethod("Data", BindingFlags.Public | BindingFlags.Static);
            if (dataMethod == null)
            {
                GULogger.Warning(Tag, $"Tangle class {typeName} has no static Data() method.");
                return null;
            }

            try
            {
                var tangleData = dataMethod.Invoke(null, null) as byte[];
                if (tangleData == null || tangleData.Length == 0)
                {
                    GULogger.Warning(Tag, $"Tangle class {typeName} was generated without credentials.");
                    return null;
                }

                return tangleData;
            }
            catch (Exception exception)
            {
                GULogger.Exception(exception, Tag);
                return null;
            }
        }

        private static Type FindType(string typeName)
        {
            var type = Type.GetType(typeName);
            if (type != null)
            {
                return type;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
