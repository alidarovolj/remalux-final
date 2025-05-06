using UnityEngine;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Управляет настройками AR камеры и переключением между AR и симуляцией
/// </summary>
public class CustomARCameraSetup : MonoBehaviour
{
    /// <summary>
    /// Камера для симуляции в редакторе
    /// </summary>
    public Camera simulationCamera;

    /// <summary>
    /// AR камера
    /// </summary>
    private Camera arCamera;

    private void Start()
    {
        // Находим AR камеру
        var xrOrigin = GetComponentInChildren<Unity.XR.CoreUtils.XROrigin>();
        if (xrOrigin != null)
        {
            arCamera = xrOrigin.Camera;
        }
        else
        {
            Debug.LogError("Не найден XROrigin! AR камера не может быть настроена");
        }

        // Если не указана SimulationCamera, найдем её по имени
        if (simulationCamera == null)
        {
            GameObject simCamObj = GameObject.Find("SimulationCamera");
            if (simCamObj != null)
            {
                simulationCamera = simCamObj.GetComponent<Camera>();
            }
        }

        // Настраиваем камеры в зависимости от режима
        SetupCameras();
    }

    private void SetupCameras()
    {
        // В редакторе используем симуляционную камеру
#if UNITY_EDITOR
        if (simulationCamera != null)
        {
            simulationCamera.enabled = true;
            simulationCamera.depth = 1; // Рендерим поверх AR камеры

            if (arCamera != null)
            {
                // Отключаем AR фон в редакторе, но оставляем камеру включенной для обработки AR
                var arCameraBackground = arCamera.GetComponent<ARCameraBackground>();
                if (arCameraBackground != null)
                {
                    arCameraBackground.enabled = false;
                }

                // Важно: не отключаем AR камеру полностью, иначе AR компоненты не будут работать
                arCamera.enabled = true;
                arCamera.depth = -1; // Рендерится под симуляционной камерой
            }
        }
#else
        // На устройстве используем только AR камеру
        if (simulationCamera != null)
        {
            simulationCamera.enabled = false;
        }
        
        if (arCamera != null)
        {
            arCamera.enabled = true;
            
            var arCameraBackground = arCamera.GetComponent<ARCameraBackground>();
            if (arCameraBackground != null)
            {
                arCameraBackground.enabled = true;
            }
        }
#endif
    }
}