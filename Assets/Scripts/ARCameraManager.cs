using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;

/// <summary>
/// Компонент для управления камерами в AR проекте
/// </summary>
public class ARCameraSetup : MonoBehaviour
{
    [SerializeField] private Camera simulationCamera;
    
    private XROrigin xrOrigin;
    private Camera arCamera;
    
    private void Awake()
    {
        // Находим XR Origin и AR Camera
        xrOrigin = FindObjectOfType<XROrigin>();
        if (xrOrigin != null)
        {
            arCamera = xrOrigin.Camera;
        }
        
        // Если не указана SimulationCamera, ищем её в сцене по имени
        if (simulationCamera == null)
        {
            GameObject simCamObj = GameObject.Find("SimulationCamera");
            if (simCamObj != null)
            {
                simulationCamera = simCamObj.GetComponent<Camera>();
            }
        }
        
        // Настраиваем камеры
        SetupCameras();
    }
    
    private void SetupCameras()
    {
        // Проверяем, запущено ли приложение на устройстве
        bool isOnDevice = Application.isEditor == false;
        
        if (isOnDevice)
        {
            // На устройстве - отключаем симуляционную камеру
            if (simulationCamera != null)
            {
                simulationCamera.gameObject.SetActive(false);
                Debug.Log("Симуляционная камера отключена на устройстве");
            }
            
            // Включаем AR Camera
            if (arCamera != null)
            {
                arCamera.gameObject.SetActive(true);
                arCamera.tag = "MainCamera";
                
                // Убеждаемся, что AR камера имеет нужные компоненты
                if (arCamera.GetComponent<ARCameraManager>() == null)
                {
                    arCamera.gameObject.AddComponent<ARCameraManager>();
                }
                
                if (arCamera.GetComponent<ARCameraBackground>() == null)
                {
                    arCamera.gameObject.AddComponent<ARCameraBackground>();
                }
            }
        }
        else
        {
            // В редакторе - используем обе камеры
            // SimulationCamera для предварительного просмотра, AR Camera для показа AR данных
            
            if (simulationCamera != null && arCamera != null)
            {
                // Настраиваем приоритет камер
                simulationCamera.depth = 0;
                arCamera.depth = -1; // AR камера рендерится под симуляционной
                
                Debug.Log("Настроены камеры для редактора");
            }
        }
    }
} 