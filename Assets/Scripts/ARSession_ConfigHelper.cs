using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Компонент для корректной настройки AR-сессии в момент старта приложения
/// </summary>
[RequireComponent(typeof(ARSession))]
public class ARSession_ConfigHelper : MonoBehaviour
{
    [Header("AR Session Settings")]
    [SerializeField] private bool runInBackground = true;
    [SerializeField] private bool attemptUpdate = true;
    [SerializeField] private bool matchFrameRate = true;
    
    [Header("Tracking Settings")]
    // Используем int вместо Feature для совместимости с Unity Editor
    [SerializeField] private int trackingFeatures = (int)(Feature.PointCloud | Feature.PlaneTracking);
    
    [Header("Debug")]
    [SerializeField] private bool logSessionEvents = true;
    
    private ARSession arSession;
    
    private void Awake()
    {
        arSession = GetComponent<ARSession>();
        
        if (arSession == null)
        {
            Debug.LogError("ARSession компонент не найден!");
            return;
        }
        
        // Установка параметра runInBackground для приложения целиком,
        // а не для ARSession напрямую
        Application.runInBackground = runInBackground;
        
        // Установка других параметров
        if (arSession.subsystem != null)
        {
            // Режим отслеживания - преобразуем целое число обратно в Feature
            arSession.subsystem.requestedTrackingMode = (Feature)trackingFeatures;
            
            // Обновление конфигурации
            if (attemptUpdate)
            {
                arSession.enabled = true;
            }
        }
        
        if (logSessionEvents)
        {
            Debug.Log($"ARSession_ConfigHelper: Сессия настроена. Application.runInBackground={Application.runInBackground}, trackingFeatures={(Feature)trackingFeatures}");
        }
    }
    
    private void OnEnable()
    {
        ARSession.stateChanged += OnARSessionStateChanged;
    }
    
    private void OnDisable()
    {
        ARSession.stateChanged -= OnARSessionStateChanged;
    }
    
    private void OnARSessionStateChanged(ARSessionStateChangedEventArgs args)
    {
        if (logSessionEvents)
        {
            Debug.Log($"ARSession_ConfigHelper: Статус сессии изменился на {args.state}");
            
            if (args.state == ARSessionState.None)
            {
                Debug.Log("ARSession_ConfigHelper: Сессия не инициализирована");
            }
            else if (args.state == ARSessionState.CheckingAvailability)
            {
                Debug.Log("ARSession_ConfigHelper: Проверка доступности AR");
            }
            else if (args.state == ARSessionState.NeedsInstall)
            {
                Debug.Log("ARSession_ConfigHelper: Требуется установка AR сервисов");
            }
            else if (args.state == ARSessionState.Installing)
            {
                Debug.Log("ARSession_ConfigHelper: Установка AR сервисов");
            }
            else if (args.state == ARSessionState.Ready)
            {
                Debug.Log("ARSession_ConfigHelper: Сессия готова к запуску");
            }
            else if (args.state == ARSessionState.SessionInitializing)
            {
                Debug.Log("ARSession_ConfigHelper: Инициализация сессии");
            }
            else if (args.state == ARSessionState.SessionTracking)
            {
                Debug.Log("ARSession_ConfigHelper: Сессия в режиме трекинга");
                
                if (arSession != null && arSession.subsystem != null)
                {
                    Debug.Log($"ARSession_ConfigHelper: Текущий режим трекинга: {arSession.subsystem.currentTrackingMode}");
                }
            }
        }
    }
} 