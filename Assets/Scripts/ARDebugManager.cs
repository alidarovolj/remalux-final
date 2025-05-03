using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;
using System.Collections.Generic;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Компонент для отображения диагностической информации в AR и помощи в отладке
/// </summary>
public class ARDebugManager : MonoBehaviour
{
    [Header("Компоненты AR")]
    [SerializeField] private ARSession arSession;
    [SerializeField] private XROrigin xrOrigin;
    [SerializeField] private ARPlaneManager planeManager;
    
    [Header("UI")]
    [SerializeField] private UnityEngine.UI.Text debugText;
    
    private void Start()
    {
        // Находим компоненты, если они не назначены
        if (arSession == null)
            arSession = FindObjectOfType<ARSession>();
            
        if (xrOrigin == null)
            xrOrigin = FindObjectOfType<XROrigin>();
            
        if (planeManager == null && xrOrigin != null)
            planeManager = xrOrigin.GetComponent<ARPlaneManager>();
            
        // Если нет UI текста для дебага, создаем его
        if (debugText == null)
        {
            CreateDebugText();
        }
    }
    
    private void Update()
    {
        if (debugText != null)
        {
            string debugInfo = GetDebugInfo();
            debugText.text = debugInfo;
        }
    }
    
    private string GetDebugInfo()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        
        sb.AppendLine("=== AR Debug Info ===");
        
        // Информация о сессии
        if (arSession != null)
        {
            sb.AppendLine($"Session State: {ARSession.state}");
            sb.AppendLine($"Session Tracking: {ARSession.notTrackingReason}");
        }
        else
        {
            sb.AppendLine("AR Session not found!");
        }
        
        sb.AppendLine();
        
        // Информация о камере
        if (xrOrigin != null && xrOrigin.Camera != null)
        {
            sb.AppendLine($"Camera Position: {xrOrigin.Camera.transform.position}");
            sb.AppendLine($"Camera Rotation: {xrOrigin.Camera.transform.eulerAngles}");
        }
        else
        {
            sb.AppendLine("AR Camera not found!");
        }
        
        sb.AppendLine();
        
        // Информация о плоскостях
        if (planeManager != null)
        {
            sb.AppendLine($"Planes detected: {planeManager.trackables.count}");
            
            // Показываем информацию о вертикальных плоскостях (стенах)
            int wallCount = 0;
            foreach (ARPlane plane in planeManager.trackables)
            {
                if (plane.alignment == PlaneAlignment.Vertical)
                {
                    wallCount++;
                }
            }
            
            sb.AppendLine($"Walls detected: {wallCount}");
        }
        else
        {
            sb.AppendLine("AR Plane Manager not found!");
        }
        
        return sb.ToString();
    }
    
    private void CreateDebugText()
    {
        // Создаем текст для отображения дебаг-информации
        GameObject textObj = new GameObject("DebugText");
        textObj.transform.SetParent(transform);
        
        Canvas canvas = GetComponentInChildren<Canvas>();
        if (canvas != null)
        {
            textObj.transform.SetParent(canvas.transform);
            
            RectTransform rectTransform = textObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 0.7f);
            rectTransform.anchorMax = new Vector2(0.4f, 1);
            rectTransform.offsetMin = new Vector2(10, 10);
            rectTransform.offsetMax = new Vector2(-10, -10);
            
            debugText = textObj.AddComponent<UnityEngine.UI.Text>();
            debugText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            debugText.fontSize = 14;
            debugText.color = Color.white;
            debugText.raycastTarget = false;
            debugText.alignment = TextAnchor.UpperLeft;
        }
    }
} 