using System;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Вспомогательный класс, который помогает работать с разными версиями AR Foundation
/// </summary>
public static class ARVersionHelper
{
    /// <summary>
    /// Получение версии AR Foundation в проекте
    /// </summary>
    public static Version GetARFoundationVersion()
    {
        try
        {
            // Проверяем наличие пакета com.unity.xr.arfoundation
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.xr.arfoundation");
            if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.version))
            {
                return new Version(packageInfo.version);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to get AR Foundation version: {ex.Message}");
        }
        
        // Если не удалось определить версию, возвращаем 5.0.0 как дефолт (для AR Foundation 5.0+)
        return new Version(5, 0, 0);
    }
    
    /// <summary>
    /// Проверка поддержки функции
    /// </summary>
    public static bool SupportsRequestedDetectionMode()
    {
        var version = GetARFoundationVersion();
        return version.Major >= 4; // requestedDetectionMode введен в AR Foundation 4.0+
    }
    
    /// <summary>
    /// Проверка, использует ли AR Foundation новый API
    /// </summary>
    public static bool UsesUpdatedAPI()
    {
        var version = GetARFoundationVersion();
        return version.Major >= 5; // XROrigin и другие изменения появились в 5.0+
    }
} 