using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(XROrigin))]
public class ARPlaneOverflowFixer : MonoBehaviour
{
    [Tooltip("AR Plane Manager, который отслеживает плоскости")]
    public ARPlaneManager planeManager;
    
    [Tooltip("XR Origin для сброса положения")]
    public XROrigin sessionOrigin;
    
    [Tooltip("Максимально допустимое значение для координаты (X, Y, Z)")]
    public float maxCoordinateValue = 100f;
    
    [Tooltip("Частота проверки координат (в секундах)")]
    public float checkInterval = 0.5f;
    
    [Tooltip("Метод исправления: 0 - сброс плоскостей, 1 - сброс AR Origin")]
    public FixMethod fixMethod = FixMethod.ResetOrigin;
    
    [Tooltip("Выполнять проверку даже если нет видимых признаков проблемы")]
    public bool proactiveChecking = true;
    
    [Tooltip("Автоматически исправлять проблемы при обнаружении")]
    public bool autoFixOnOverflow = true;
    
    [Tooltip("Минимальное время между исправлениями (секунды)")]
    public float minTimeBetweenFixes = 1.0f;
    
    private float lastCheckTime = 0f;
    private float lastFixTime = 0f;
    
    public enum FixMethod
    {
        ResetPlanes,
        ResetOrigin
    }
    
    void Start()
    {
        if (planeManager == null)
        {
            planeManager = FindObjectOfType<ARPlaneManager>();
            if (planeManager == null)
            {
                Debug.LogError("ARPlaneOverflowFixer: ARPlaneManager не найден. Компонент будет отключен.");
                enabled = false;
                return;
            }
        }
        
        if (sessionOrigin == null)
        {
            sessionOrigin = GetComponent<XROrigin>();
            if (sessionOrigin == null)
            {
                sessionOrigin = FindObjectOfType<XROrigin>();
            }
            
            if (sessionOrigin == null)
            {
                Debug.LogError("ARPlaneOverflowFixer: XROrigin не найден. Сброс Origin не будет работать.");
            }
        }
        
        // Подписываемся на событие изменения плоскостей
        planeManager.planesChanged += OnPlanesChanged;
        
        // Подписываемся на событие изменения состояния сессии
        ARSession.stateChanged += OnARSessionStateChanged;
        
        Debug.Log("ARPlaneOverflowFixer: Инициализирован. Будет отслеживать переполнение координат плоскостей.");
    }
    
    void OnDestroy()
    {
        if (planeManager != null)
        {
            planeManager.planesChanged -= OnPlanesChanged;
        }
        
        ARSession.stateChanged -= OnARSessionStateChanged;
    }
    
    private void OnARSessionStateChanged(ARSessionStateChangedEventArgs args)
    {
        // Проверяем только в режиме трекинга
        if (args.state == ARSessionState.SessionTracking)
        {
            CheckAndFixCoordinateOverflow();
        }
    }
    
    private void OnPlanesChanged(ARPlanesChangedEventArgs args)
    {
        // При добавлении новых плоскостей проверяем переполнение координат
        if (args.added != null && args.added.Count > 0)
        {
            CheckAndFixCoordinateOverflow();
        }
    }
    
    void Update()
    {
        // Регулярно проверяем все плоскости
        if (proactiveChecking && Time.time - lastCheckTime > checkInterval)
        {
            lastCheckTime = Time.time;
            CheckAndFixCoordinateOverflow();
        }
    }
    
    public void CheckAndFixCoordinateOverflow()
    {
        // Проверяем, можем ли мы выполнить фикс (достаточно ли времени прошло с последнего исправления)
        bool canFix = Time.time - lastFixTime >= minTimeBetweenFixes;
        
        if (AnyPlaneHasCoordinateOverflow())
        {
            Debug.LogWarning($"ARPlaneOverflowFixer: Обнаружено переполнение координат плоскостей (>{maxCoordinateValue}).");
            
            if (autoFixOnOverflow && canFix)
            {
                FixCoordinateOverflow();
                lastFixTime = Time.time;
            }
        }
    }
    
    private bool AnyPlaneHasCoordinateOverflow()
    {
        if (planeManager == null || planeManager.trackables.count == 0)
            return false;
        
        foreach (var plane in planeManager.trackables)
        {
            Vector3 center = plane.center;
            if (Mathf.Abs(center.x) > maxCoordinateValue || 
                Mathf.Abs(center.y) > maxCoordinateValue || 
                Mathf.Abs(center.z) > maxCoordinateValue)
            {
                Debug.LogWarning($"ARPlaneOverflowFixer: Плоскость {plane.trackableId} имеет переполнение координат: {center}");
                return true;
            }
            
            // Проверяем также вершины меша плоскости
            var meshFilter = plane.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.mesh != null)
            {
                foreach (var vertex in meshFilter.mesh.vertices)
                {
                    Vector3 worldVertex = plane.transform.TransformPoint(vertex);
                    if (Mathf.Abs(worldVertex.x) > maxCoordinateValue || 
                        Mathf.Abs(worldVertex.y) > maxCoordinateValue || 
                        Mathf.Abs(worldVertex.z) > maxCoordinateValue)
                    {
                        Debug.LogWarning($"ARPlaneOverflowFixer: Плоскость {plane.trackableId} имеет вершину с переполнением координат: {worldVertex}");
                        return true;
                    }
                }
            }
        }
        
        return false;
    }
    
    public void FixCoordinateOverflow()
    {
        switch (fixMethod)
        {
            case FixMethod.ResetOrigin:
                ResetAROrigin();
                break;
            case FixMethod.ResetPlanes:
                ResetPlanesCoordinates();
                break;
        }
    }
    
    private void ResetAROrigin()
    {
        if (sessionOrigin == null)
        {
            Debug.LogError("ARPlaneOverflowFixer: Не удается выполнить сброс AR Origin, так как ссылка на XROrigin не установлена.");
            return;
        }
        
        // Сохраняем текущую позицию камеры
        Camera arCamera = sessionOrigin.Camera;
        if (arCamera == null)
        {
            Debug.LogError("ARPlaneOverflowFixer: Не удается выполнить сброс AR Origin, так как ссылка на AR Camera не установлена.");
            return;
        }
        
        Vector3 cameraPosition = arCamera.transform.position;
        Quaternion cameraRotation = arCamera.transform.rotation;
        
        // Сбрасываем позицию XR Origin
        Vector3 oldPosition = sessionOrigin.transform.position;
        sessionOrigin.transform.position = Vector3.zero;
        
        // Смещаем сессию в противоположном направлении, чтобы камера осталась на том же месте
        Vector3 offset = oldPosition - cameraPosition;
        sessionOrigin.transform.position = offset;
        
        Debug.Log($"ARPlaneOverflowFixer: Выполнен сброс AR Origin. Старая позиция: {oldPosition}, новая позиция: {sessionOrigin.transform.position}");
        
        // Принудительно запускаем обновление всех плоскостей
        ARPlaneController planeController = FindObjectOfType<ARPlaneController>();
        if (planeController != null)
        {
            planeController.ForceUpdateAllPlanes();
            Debug.Log("ARPlaneOverflowFixer: Запущено принудительное обновление всех плоскостей");
        }
    }
    
    private void ResetPlanesCoordinates()
    {
        int resetCount = 0;
        
        foreach (var plane in planeManager.trackables)
        {
            ARPlaneVisualizer visualizer = plane.GetComponentInChildren<ARPlaneVisualizer>();
            if (visualizer != null)
            {
                // Ищем метод ResetPlanePosition с помощью reflection
                System.Reflection.MethodInfo resetMethod = visualizer.GetType().GetMethod("ResetPlanePosition", 
                    System.Reflection.BindingFlags.Instance | 
                    System.Reflection.BindingFlags.Public | 
                    System.Reflection.BindingFlags.NonPublic);
                
                if (resetMethod != null)
                {
                    resetMethod.Invoke(visualizer, null);
                    resetCount++;
                }
                else
                {
                    // Если метод не найден, пробуем установить позицию визуализатора напрямую
                    visualizer.transform.localPosition = Vector3.zero;
                    visualizer.transform.localRotation = Quaternion.identity;
                    
                    // Вызываем UpdateVisual если метод существует
                    System.Reflection.MethodInfo updateMethod = visualizer.GetType().GetMethod("UpdateVisual", 
                        System.Reflection.BindingFlags.Instance | 
                        System.Reflection.BindingFlags.Public);
                    
                    if (updateMethod != null)
                    {
                        updateMethod.Invoke(visualizer, null);
                    }
                    
                    resetCount++;
                }
            }
        }
        
        Debug.Log($"ARPlaneOverflowFixer: Выполнен сброс координат для {resetCount} плоскостей");
    }
} 