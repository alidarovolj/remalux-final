using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;

public class ARWallPaintingApp : MonoBehaviour
{
    [Header("AR Components")]
    [SerializeField] private ARSession arSession;
    [SerializeField] private XROrigin xrOrigin;
    [SerializeField] private ARPlaneManager planeManager;
    [SerializeField] private ARRaycastManager raycastManager;
    
    [Header("App Components")]
    [SerializeField] private WallSegmentation wallSegmentation;
    [SerializeField] private WallPainter wallPainter;
    [SerializeField] private UIManager uiManager;
    
    [Header("Settings")]
    [SerializeField] private bool showPlaneVisualizers = true;
    [SerializeField] private Material wallPaintMaterial;
    
    // Состояние приложения
    private enum AppState
    {
        Initializing,
        ScanningEnvironment,
        SegmentingWalls,
        ReadyToPaint
    }
    
    private AppState currentState = AppState.Initializing;
    
    private void Start()
    {
        // Получаем ссылки на компоненты, если они не назначены
        if (arSession == null)
            arSession = Object.FindAnyObjectByType<ARSession>();
            
        if (xrOrigin == null)
            xrOrigin = Object.FindAnyObjectByType<XROrigin>();
            
        if (planeManager == null)
            planeManager = Object.FindAnyObjectByType<ARPlaneManager>();
            
        if (raycastManager == null)
            raycastManager = Object.FindAnyObjectByType<ARRaycastManager>();
            
        if (wallSegmentation == null)
            wallSegmentation = Object.FindAnyObjectByType<WallSegmentation>();
            
        if (wallPainter == null)
            wallPainter = Object.FindAnyObjectByType<WallPainter>();
            
        if (uiManager == null)
            uiManager = Object.FindAnyObjectByType<UIManager>();
        
        // Настройка отображения плоскостей - отключаем стандартные плоскости
        showPlaneVisualizers = false;
        
        if (planeManager != null)
        {
            planeManager.planesChanged += OnPlanesChanged;
            
            // Установка видимости плоскостей
            foreach (var plane in planeManager.trackables)
            {
                plane.gameObject.SetActive(showPlaneVisualizers);
            }
        }
        
        // Находим и используем ARPlaneController для скрытия стандартных плоскостей
        ARPlaneController planeController = FindObjectOfType<ARPlaneController>();
        if (planeController != null)
        {
            planeController.HideDefaultPlanes();
        }
        else
        {
            Debug.LogWarning("ARPlaneController не найден. Стандартные плоскости могут остаться видимыми.");
        }
        
        // Указываем начальное состояние
        SetAppState(AppState.Initializing);
        
        // Запускаем процесс инициализации
        StartCoroutine(InitializeApp());
    }
    
    // Инициализация приложения
    private IEnumerator InitializeApp()
    {
        // Ждем инициализации AR сессии
        yield return new WaitForSeconds(0.5f);
        
        // Переходим к сканированию окружения
        SetAppState(AppState.ScanningEnvironment);
        
        // Ждем обнаружения достаточного количества плоскостей
        yield return new WaitUntil(() => HasEnoughPlanes());
        
        // Переходим к сегментации стен
        SetAppState(AppState.SegmentingWalls);
        
        // Ждем завершения сегментации (можно задать условие)
        yield return new WaitForSeconds(2.0f);
        
        // Переходим в состояние готовности к покраске
        SetAppState(AppState.ReadyToPaint);
    }
    
    // Проверка наличия достаточного количества плоскостей
    private bool HasEnoughPlanes()
    {
        int verticalPlanes = 0;
        
        foreach (var plane in planeManager.trackables)
        {
            if (plane.alignment == UnityEngine.XR.ARSubsystems.PlaneAlignment.Vertical)
            {
                verticalPlanes++;
            }
        }
        
        // Считаем, что достаточно хотя бы 1 вертикальной плоскости
        return verticalPlanes >= 1;
    }
    
    // Обработчик события изменения плоскостей
    private void OnPlanesChanged(ARPlanesChangedEventArgs args)
    {
        // Обновляем видимость плоскостей
        foreach (var plane in args.added)
        {
            plane.gameObject.SetActive(showPlaneVisualizers);
        }
        
        // Если обнаружены новые вертикальные плоскости в нужном состоянии
        if (currentState == AppState.ScanningEnvironment)
        {
            foreach (var plane in args.added)
            {
                if (plane.alignment == UnityEngine.XR.ARSubsystems.PlaneAlignment.Vertical)
                {
                    // Обновляем UI-информацию о том, что найдены стены
                    if (uiManager != null)
                    {
                        uiManager.UpdateWallDetectionStatus(true);
                    }
                }
            }
        }
    }
    
    // Установка состояния приложения
    private void SetAppState(AppState newState)
    {
        currentState = newState;
        
        switch (newState)
        {
            case AppState.Initializing:
                if (uiManager != null)
                {
                    uiManager.UpdateWallDetectionStatus(false);
                }
                break;
                
            case AppState.ScanningEnvironment:
                if (uiManager != null)
                {
                    uiManager.UpdateWallDetectionStatus(false);
                }
                break;
                
            case AppState.SegmentingWalls:
                break;
                
            case AppState.ReadyToPaint:
                if (uiManager != null)
                {
                    uiManager.UpdateWallDetectionStatus(true);
                }
                break;
        }
    }
    
    // Переключение видимости визуализаторов плоскостей
    public void TogglePlaneVisualization()
    {
        showPlaneVisualizers = !showPlaneVisualizers;
        
        foreach (var plane in planeManager.trackables)
        {
            plane.gameObject.SetActive(showPlaneVisualizers);
        }
    }
    
    private void OnDestroy()
    {
        if (planeManager != null)
        {
            planeManager.planesChanged -= OnPlanesChanged;
        }
    }
} 