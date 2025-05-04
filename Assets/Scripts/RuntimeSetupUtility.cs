using UnityEngine;
using System.Collections;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Утилита для настройки AR-сцены в рантайме
/// </summary>
public class RuntimeSetupUtility : MonoBehaviour
{
    [Header("Ссылки на ресурсы")]
    [SerializeField] private Material wallPaintMaterial;
    [SerializeField] private GameObject wallPrefab;
    
    // Start вызывается при старте приложения
    private void Start()
    {
        StartCoroutine(SetupARScene());
    }
    
    // Настройка AR-сцены
    private IEnumerator SetupARScene()
    {
        Debug.Log("Начинаем настройку AR-сцены...");
        
        // Проверяем и создаем основные AR-компоненты
        yield return StartCoroutine(SetupARComponents());
        
        // Настраиваем компоненты для сегментации и рисования
        yield return StartCoroutine(SetupWallComponents());
        
        // Настраиваем UI
        yield return StartCoroutine(SetupUI());
        
        Debug.Log("Настройка AR-сцены завершена!");
    }
    
    // Настройка основных AR-компонентов
    private IEnumerator SetupARComponents()
    {
        Debug.Log("Настройка основных AR-компонентов...");
        
        // Проверяем AR Session
        ARSession arSession = FindObjectOfType<ARSession>();
        if (arSession == null)
        {
            GameObject sessionObj = new GameObject("AR Session");
            arSession = sessionObj.AddComponent<ARSession>();
            Debug.Log("Создан AR Session");
            
            // Добавляем помощник конфигурации
            ARSession_ConfigHelper configHelper = sessionObj.AddComponent<ARSession_ConfigHelper>();
            
            // Устанавливаем глобальный параметр runInBackground
            Application.runInBackground = true; 
            
            Debug.Log("Добавлен ARSession_ConfigHelper для корректной настройки сессии");
        }
        else if (!arSession.gameObject.GetComponent<ARSession_ConfigHelper>())
        {
            // Добавляем помощник конфигурации, если его нет
            ARSession_ConfigHelper configHelper = arSession.gameObject.AddComponent<ARSession_ConfigHelper>();
            
            // Устанавливаем глобальный параметр runInBackground
            Application.runInBackground = true;
            
            Debug.Log("Добавлен ARSession_ConfigHelper для корректной настройки сессии");
        }
        
        // Проверяем XR Origin
        XROrigin xrOrigin = FindObjectOfType<XROrigin>();
        if (xrOrigin == null)
        {
            GameObject originObj = new GameObject("XR Origin");
            xrOrigin = originObj.AddComponent<XROrigin>();
            
            // Создаем камеру внутри XR Origin
            GameObject cameraObj = new GameObject("AR Camera");
            cameraObj.transform.SetParent(originObj.transform);
            Camera arCamera = cameraObj.AddComponent<Camera>();
            cameraObj.AddComponent<ARCameraManager>();
            cameraObj.AddComponent<ARCameraBackground>();
            
            xrOrigin.Camera = arCamera;
            Debug.Log("Создан XR Origin с камерой");
        }
        
        // Проверяем AR Plane Manager
        ARPlaneManager planeManager = FindObjectOfType<ARPlaneManager>();
        if (planeManager == null && xrOrigin != null)
        {
            planeManager = xrOrigin.gameObject.AddComponent<ARPlaneManager>();
            Debug.Log("Создан AR Plane Manager");
        }
        
        // Проверяем AR Raycast Manager
        ARRaycastManager raycastManager = FindObjectOfType<ARRaycastManager>();
        if (raycastManager == null && xrOrigin != null)
        {
            raycastManager = xrOrigin.gameObject.AddComponent<ARRaycastManager>();
            Debug.Log("Создан AR Raycast Manager");
        }
        
        yield return null;
    }
    
    // Настройка компонентов для сегментации и рисования стен
    private IEnumerator SetupWallComponents()
    {
        Debug.Log("Настройка компонентов для работы со стенами...");
        
        // Проверяем AppController
        ARWallPaintingApp appController = FindObjectOfType<ARWallPaintingApp>();
        if (appController == null)
        {
            GameObject appObj = new GameObject("AppController");
            appController = appObj.AddComponent<ARWallPaintingApp>();
            Debug.Log("Создан AppController");
        }
        
        // Проверяем Wall Segmentation
        WallSegmentation wallSegmentation = FindObjectOfType<WallSegmentation>();
        if (wallSegmentation == null)
        {
            GameObject segmentationObj = new GameObject("WallSegmentationManager");
            wallSegmentation = segmentationObj.AddComponent<WallSegmentation>();
            Debug.Log("Создан WallSegmentationManager");
            
            // Устанавливаем демо-режим по умолчанию
            // Получить доступ к приватному полю через reflection или настроить публичное API
            wallSegmentation.SwitchMode(WallSegmentation.SegmentationMode.Demo);
        }
        
        // Проверяем Wall Painter
        WallPainter wallPainter = FindObjectOfType<WallPainter>();
        if (wallPainter == null)
        {
            GameObject painterObj = new GameObject("WallPainterManager");
            wallPainter = painterObj.AddComponent<WallPainter>();
            
            // Назначаем префаб стены и материал, если они есть
            if (wallPrefab != null)
            {
                var painter = wallPainter.GetComponent<WallPainter>();
                // Используем reflection для доступа к приватному полю
                SetObjectReference(painter, "wallPrefab", wallPrefab);
            }
                
            if (wallPaintMaterial != null)
            {
                var painter = wallPainter.GetComponent<WallPainter>();
                // Используем reflection для доступа к приватному полю
                SetObjectReference(painter, "wallMaterial", wallPaintMaterial);
            }
                
            Debug.Log("Создан WallPainterManager");
        }
        
        // Связываем компоненты
        if (appController != null)
        {
            ARSession arSession = FindObjectOfType<ARSession>();
            XROrigin xrOrigin = FindObjectOfType<XROrigin>();
            ARPlaneManager planeManager = FindObjectOfType<ARPlaneManager>();
            ARRaycastManager raycastManager = FindObjectOfType<ARRaycastManager>();
            UIManager uiManager = FindObjectOfType<UIManager>();
            
            // Использование reflection или публичного API для установки ссылок
            SetObjectReference(appController, "arSession", arSession);
            SetObjectReference(appController, "xrOrigin", xrOrigin);
            SetObjectReference(appController, "planeManager", planeManager);
            SetObjectReference(appController, "raycastManager", raycastManager);
            SetObjectReference(appController, "wallSegmentation", wallSegmentation);
            SetObjectReference(appController, "wallPainter", wallPainter);
            SetObjectReference(appController, "uiManager", uiManager);
        }
        
        yield return null;
    }
    
    // Настройка пользовательского интерфейса
    private IEnumerator SetupUI()
    {
        Debug.Log("Настройка пользовательского интерфейса...");
        
        // Проверяем Canvas
        Canvas mainCanvas = FindObjectOfType<Canvas>();
        if (mainCanvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas");
            mainCanvas = canvasObj.AddComponent<Canvas>();
            mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            Debug.Log("Создан Canvas");
            
            // Проверяем UI Manager
            UIManager uiManager = canvasObj.GetComponent<UIManager>();
            if (uiManager == null)
            {
                uiManager = canvasObj.AddComponent<UIManager>();
                Debug.Log("Создан UI Manager");
            }
            
            // Связываем UI Manager с другими компонентами
            WallPainter wallPainter = FindObjectOfType<WallPainter>();
            WallSegmentation wallSegmentation = FindObjectOfType<WallSegmentation>();
            
            if (uiManager != null)
            {
                SetObjectReference(uiManager, "wallPainter", wallPainter);
                SetObjectReference(uiManager, "wallSegmentation", wallSegmentation);
            }
            
            // Добавляем основные UI элементы, если их нет
            // Это упрощенная реализация, в идеале лучше использовать префабы
            
            // Статусный текст
            GameObject statusTextObj = new GameObject("StatusText");
            statusTextObj.transform.SetParent(canvasObj.transform);
            TextMeshProUGUI statusText = statusTextObj.AddComponent<TextMeshProUGUI>();
            statusText.text = "Сканируйте окружение...";
            statusText.color = Color.white;
            statusText.fontSize = 24;
            RectTransform statusRect = statusText.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0, 0.9f);
            statusRect.anchorMax = new Vector2(1, 1);
            statusRect.offsetMin = new Vector2(10, 0);
            statusRect.offsetMax = new Vector2(-10, 0);
            
            if (uiManager != null)
            {
                SetObjectReference(uiManager, "statusText", statusText);
            }
        }
        
        yield return null;
    }
    
    // Вспомогательный метод для установки значения поля через reflection
    private void SetObjectReference(object target, string fieldName, object value)
    {
        System.Reflection.FieldInfo field = target.GetType().GetField(fieldName, 
            System.Reflection.BindingFlags.Public | 
            System.Reflection.BindingFlags.NonPublic | 
            System.Reflection.BindingFlags.Instance);
            
        if (field != null)
        {
            field.SetValue(target, value);
        }
        else
        {
            Debug.LogWarning($"Поле {fieldName} не найдено в объекте типа {target.GetType().Name}");
        }
    }
} 