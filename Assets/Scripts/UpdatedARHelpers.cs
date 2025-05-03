using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;
using Unity.XR.CoreUtils;

/// <summary>
/// Содержит вспомогательные методы для работы с AR Foundation 6.x
/// </summary>
public static class UpdatedARHelpers
{
    /// <summary>
    /// Выполняет рейкаст в AR-сцене и возвращает точку пересечения
    /// </summary>
    public static bool TryGetTouchPosition(out Vector2 touchPosition)
    {
        touchPosition = default;
        
        if (Input.touchCount == 0)
            return false;
            
        touchPosition = Input.GetTouch(0).position;
        return true;
    }
    
    /// <summary>
    /// Выполняет рейкаст AR для определения точки в пространстве
    /// </summary>
    public static bool ARRaycast(ARRaycastManager raycastManager, Vector2 screenPosition, out ARRaycastHit hitResult)
    {
        List<ARRaycastHit> hits = new List<ARRaycastHit>();
        hitResult = default;
        
        if (!raycastManager) return false;
        
        // В новой версии трассируем только по trackable-плоскостям
        if (raycastManager.Raycast(screenPosition, hits, TrackableType.PlaneWithinPolygon))
        {
            // Сортируем результаты по расстоянию
            hits.Sort((x, y) => x.distance.CompareTo(y.distance));
            hitResult = hits[0];
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Находит XROrigin в сцене
    /// </summary>
    public static XROrigin GetXROrigin()
    {
        return Object.FindAnyObjectByType<XROrigin>();
    }
    
    /// <summary>
    /// Получает AR-камеру из XROrigin
    /// </summary>
    public static Camera GetARCamera()
    {
        XROrigin origin = GetXROrigin();
        return origin ? origin.Camera : null;
    }
    
    /// <summary>
    /// Получает родительский объект для trackables в XROrigin
    /// </summary>
    public static Transform GetTrackablesParent()
    {
        XROrigin origin = GetXROrigin();
        return origin ? origin.TrackablesParent : null;
    }
    
    /// <summary>
    /// Находит ARPlaneManager
    /// </summary>
    public static ARPlaneManager GetPlaneManager()
    {
        return Object.FindAnyObjectByType<ARPlaneManager>();
    }
    
    /// <summary>
    /// Находит ARRaycastManager
    /// </summary>
    public static ARRaycastManager GetRaycastManager()
    {
        return Object.FindAnyObjectByType<ARRaycastManager>();
    }
    
    /// <summary>
    /// Переключает видимость плоскостей
    /// </summary>
    public static void TogglePlanesVisibility(bool visible)
    {
        ARPlaneManager planeManager = GetPlaneManager();
        if (!planeManager) return;
        
        foreach (ARPlane plane in planeManager.trackables)
        {
            plane.gameObject.SetActive(visible);
        }
    }
    
    /// <summary>
    /// Обработчик события изменения trackables для ARPlaneManager
    /// (замена устаревшего planesChanged)
    /// </summary>
    public static void OnTrackablesChanged<T>(
        ARTrackablesChangedEventArgs<T> args,
        System.Action<IReadOnlyList<T>> onAdded = null,
        System.Action<IReadOnlyList<T>> onUpdated = null,
        System.Action<IReadOnlyList<System.Collections.Generic.KeyValuePair<UnityEngine.XR.ARSubsystems.TrackableId, T>>> onRemoved = null) where T : ARTrackable
    {
        if (onAdded != null && args.added != null && args.added.Count > 0)
            onAdded(args.added);
            
        if (onUpdated != null && args.updated != null && args.updated.Count > 0)
            onUpdated(args.updated);
            
        if (onRemoved != null && args.removed != null && args.removed.Count > 0)
            onRemoved(args.removed);
    }
} 