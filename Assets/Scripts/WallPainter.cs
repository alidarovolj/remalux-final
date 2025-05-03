using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class WallPainter : MonoBehaviour
{
    [Header("AR Components")]
    [SerializeField] private ARRaycastManager raycastManager;
    [SerializeField] private ARSessionOrigin sessionOrigin;
    [SerializeField] private Camera arCamera;
    
    [Header("Painting")]
    [SerializeField] private GameObject wallPrefab;
    [SerializeField] private Color currentColor = Color.red;
    [SerializeField] private float brushSize = 0.2f;
    [SerializeField] private float brushIntensity = 0.8f;
    
    [Header("References")]
    [SerializeField] private WallSegmentation wallSegmentation;
    
    // Приватные переменные
    private List<ARRaycastHit> raycastHits = new List<ARRaycastHit>();
    private Dictionary<TrackableId, GameObject> paintedWalls = new Dictionary<TrackableId, GameObject>();
    private bool isPainting = false;
    
    private void Start()
    {
        if (raycastManager == null)
            raycastManager = FindObjectOfType<ARRaycastManager>();
            
        if (sessionOrigin == null)
            sessionOrigin = FindObjectOfType<ARSessionOrigin>();
            
        if (arCamera == null)
            arCamera = sessionOrigin.camera;
            
        if (wallSegmentation == null)
            wallSegmentation = FindObjectOfType<WallSegmentation>();
    }
    
    private void Update()
    {
        // Проверяем, нажимает ли пользователь на экран
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            
            // Начало касания
            if (touch.phase == TouchPhase.Began)
            {
                isPainting = true;
            }
            // Конец касания
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                isPainting = false;
            }
            
            // Если касание активно, выполняем покраску
            if (isPainting)
            {
                PaintAtPosition(touch.position);
            }
        }
    }
    
    // Покраска в точке касания
    private void PaintAtPosition(Vector2 screenPosition)
    {
        // Используем ARRaycast для определения точки в 3D-пространстве
        if (raycastManager.Raycast(screenPosition, raycastHits, TrackableType.PlaneWithinPolygon))
        {
            foreach (ARRaycastHit hit in raycastHits)
            {
                // Получаем трекаемую плоскость
                ARPlane plane = sessionOrigin.trackablesParent
                    .GetComponentsInChildren<ARPlane>()
                    .FirstOrDefault(p => p.trackableId == hit.trackableId);
                    
                if (plane != null)
                {
                    // Проверяем, является ли плоскость стеной
                    if (IsWall(plane))
                    {
                        // Применяем краску к стене
                        ApplyPaintToWall(plane, hit.pose.position);
                    }
                }
            }
        }
    }
    
    // Проверяем, является ли плоскость стеной
    private bool IsWall(ARPlane plane)
    {
        // Примерная проверка - стены обычно вертикальные
        return plane.alignment == PlaneAlignment.Vertical;
        
        // Более сложная логика может использовать данные из WallSegmentation
        // Например, проверять соответствие точек плоскости маске сегментации
    }
    
    // Применение краски к стене
    private void ApplyPaintToWall(ARPlane plane, Vector3 hitPosition)
    {
        GameObject wallObject;
        
        // Если эта стена уже покрашена, используем существующий объект
        if (!paintedWalls.TryGetValue(plane.trackableId, out wallObject))
        {
            // Создаем новый объект для покраски стены
            wallObject = Instantiate(wallPrefab, plane.transform);
            wallObject.transform.localPosition = Vector3.zero;
            wallObject.transform.localRotation = Quaternion.identity;
            
            // Настраиваем меш, чтобы соответствовал форме плоскости
            MeshFilter meshFilter = wallObject.GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                meshFilter.mesh = plane.GetComponent<MeshFilter>().mesh;
            }
            
            // Настраиваем материал
            MeshRenderer renderer = wallObject.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material material = new Material(Shader.Find("Unlit/Transparent"));
                material.color = new Color(currentColor.r, currentColor.g, currentColor.b, 0);
                renderer.material = material;
            }
            
            // Добавляем в словарь
            paintedWalls.Add(plane.trackableId, wallObject);
        }
        
        // Постепенно увеличиваем непрозрачность в месте касания
        MeshRenderer wallRenderer = wallObject.GetComponent<MeshRenderer>();
        if (wallRenderer != null)
        {
            Color currentMatColor = wallRenderer.material.color;
            Color targetColor = new Color(currentColor.r, currentColor.g, currentColor.b, brushIntensity);
            
            // Плавно изменяем цвет
            wallRenderer.material.color = Color.Lerp(currentMatColor, targetColor, Time.deltaTime * 5f);
        }
    }
    
    // Установка текущего цвета кисти
    public void SetColor(Color newColor)
    {
        currentColor = newColor;
    }
    
    // Установка размера кисти
    public void SetBrushSize(float size)
    {
        brushSize = Mathf.Clamp(size, 0.05f, 0.5f);
    }
    
    // Установка интенсивности кисти
    public void SetBrushIntensity(float intensity)
    {
        brushIntensity = Mathf.Clamp01(intensity);
    }
    
    // Сброс всей покраски
    public void ResetPainting()
    {
        foreach (var wall in paintedWalls.Values)
        {
            Destroy(wall);
        }
        
        paintedWalls.Clear();
    }
} 