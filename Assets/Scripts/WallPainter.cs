using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.XR.CoreUtils;

/// <summary>
/// Компонент для покраски стен в AR
/// </summary>
public class WallPainter : MonoBehaviour
{
    [Header("AR Components")]
    [SerializeField] private ARRaycastManager raycastManager;
    [SerializeField] private XROrigin xrOrigin;
    [SerializeField] private Camera arCamera;
    
    [Header("Painting")]
    [SerializeField] private GameObject wallPrefab;
    [SerializeField] private Material wallMaterial;
    [SerializeField] private Color currentColor = Color.red;
    [SerializeField] private float brushSize = 0.2f;
    [SerializeField] private float brushIntensity = 0.8f;
    
    [Header("References")]
    [SerializeField] private WallSegmentation wallSegmentation;
    
    [Header("Snapshots")]
    [SerializeField] private int maxSnapshots = 5;
    
    // Приватные переменные
    private List<ARRaycastHit> raycastHits = new List<ARRaycastHit>();
    private Dictionary<TrackableId, GameObject> paintedWalls = new Dictionary<TrackableId, GameObject>();
    private Dictionary<TrackableId, Color> wallColors = new Dictionary<TrackableId, Color>();
    private Dictionary<TrackableId, float> wallIntensities = new Dictionary<TrackableId, float>();
    private bool isPainting = false;
    
    // Хранение снимков
    private List<PaintSnapshot> savedSnapshots = new List<PaintSnapshot>();
    private PaintSnapshot activeSnapshot = null;
    private int currentSnapshotIndex = -1;
    
    // Делегаты для событий
    public delegate void OnSnapshotsChangedDelegate(List<PaintSnapshot> snapshots, int activeIndex);
    public event OnSnapshotsChangedDelegate OnSnapshotsChanged;
    
    // Класс для хранения данных о покрашенной стене
    private class PaintedWallData
    {
        public GameObject wallObject;
        public Material material;
        public ARPlane plane;
        
        public PaintedWallData(GameObject obj, Material mat, ARPlane p)
        {
            wallObject = obj;
            material = mat;
            plane = p;
        }
    }
    
    private void Start()
    {
        // Проверяем и настраиваем необходимые компоненты
        if (wallPrefab == null)
        {
            Debug.LogWarning("Wall Prefab не назначен, пытаемся найти его в Resources или создать по умолчанию");
            wallPrefab = Resources.Load<GameObject>("Prefabs/PaintedWall");
            
            if (wallPrefab == null)
            {
                // Создаем базовый префаб в случае отсутствия
                wallPrefab = GameObject.CreatePrimitive(PrimitiveType.Quad);
                wallPrefab.name = "DefaultWallPrefab";
            }
        }
        
        if (wallMaterial == null)
        {
            Debug.LogWarning("Wall Material не назначен, создаем материал по умолчанию");
            wallMaterial = new Material(Shader.Find("Standard"));
            wallMaterial.color = Color.white;
        }
        
        // Инициализируем цвет по умолчанию, если не задан
        if (currentColor == Color.clear)
        {
            currentColor = new Color(1f, 0f, 0f, 0.8f); // Красный полупрозрачный по умолчанию
        }
        
        // Получаем необходимые AR компоненты, если они не назначены
        if (raycastManager == null)
            raycastManager = Object.FindAnyObjectByType<ARRaycastManager>();
            
        if (xrOrigin == null)
            xrOrigin = Object.FindAnyObjectByType<XROrigin>();
            
        if (arCamera == null && xrOrigin != null)
            arCamera = xrOrigin.Camera;
            
        if (wallSegmentation == null)
            wallSegmentation = Object.FindAnyObjectByType<WallSegmentation>();
        
        // Создаем первый снимок
        CreateNewSnapshot("Исходный вариант");
    }
    
    public void StartPainting()
    {
        isPainting = true;
    }
    
    public void StopPainting()
    {
        isPainting = false;
    }
    
    private void Update()
    {
        // Проверяем состояние покраски (например, нажата ли кнопка)
        if (isPainting)
        {
            // Выполняем рейкаст для определения, куда направлена камера
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            
            if (raycastManager.Raycast(screenCenter, raycastHits, TrackableType.PlaneWithinPolygon))
            {
                // Берем первое попадание
                var hit = raycastHits[0];
                var planeId = hit.trackableId;
                
                // Проверяем, является ли плоскость вертикальной (стеной)
                ARPlane plane = raycastManager.GetComponent<ARPlaneManager>()?.GetPlane(planeId);
                
                if (plane != null && plane.alignment == PlaneAlignment.Vertical)
                {
                    // Красим стену
                    PaintWall(plane, hit.pose);
                }
            }
        }
    }
    
    private void PaintWall(ARPlane plane, Pose hitPose)
    {
        // Проверяем, существует ли уже объект для этой плоскости
        if (!paintedWalls.TryGetValue(plane.trackableId, out GameObject wallObject))
        {
            // Создаем новый объект стены
            wallObject = Instantiate(wallPrefab, hitPose.position, hitPose.rotation);
            wallObject.transform.parent = plane.transform;
            
            // Масштабируем объект в соответствии с размерами плоскости
            Bounds planeBounds = plane.GetComponent<MeshFilter>().mesh.bounds;
            wallObject.transform.localScale = new Vector3(
                planeBounds.size.x,
                planeBounds.size.y,
                1.0f
            );
            
            // Создаем новый материал для этой стены
            MeshRenderer wallRenderer = wallObject.GetComponent<MeshRenderer>();
            wallRenderer.material = new Material(wallMaterial);
            
            // Добавляем созданный объект в словарь
            paintedWalls.Add(plane.trackableId, wallObject);
            
            // Устанавливаем начальный цвет и интенсивность
            wallColors[plane.trackableId] = currentColor;
            wallIntensities[plane.trackableId] = brushIntensity;
        }
        else
        {
            // Обновляем цвет существующего объекта
            MeshRenderer wallRenderer = wallObject.GetComponent<MeshRenderer>();
            
            // Проверяем, не изменилась ли плоскость
            Bounds planeBounds = plane.GetComponent<MeshFilter>().mesh.bounds;
            wallObject.transform.localScale = new Vector3(
                planeBounds.size.x,
                planeBounds.size.y,
                1.0f
            );
            
            // Применяем новый цвет с учетом кисти
            Color currentMatColor = wallRenderer.material.color;
            Color targetColor = new Color(currentColor.r, currentColor.g, currentColor.b, brushIntensity);
            
            // Плавно изменяем цвет
            wallRenderer.material.color = Color.Lerp(currentMatColor, targetColor, Time.deltaTime * 5f);
            
            // Сохраняем текущий цвет и интенсивность для каждой стены
            wallColors[plane.trackableId] = new Color(currentColor.r, currentColor.g, currentColor.b);
            wallIntensities[plane.trackableId] = brushIntensity;
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
        wallColors.Clear();
        wallIntensities.Clear();
        
        // Создаем новый снимок после сброса
        CreateNewSnapshot("Новый вариант");
    }
    
    // Создание нового снимка текущего состояния
    public void CreateNewSnapshot(string name)
    {
        // Создаем новый снимок
        PaintSnapshot snapshot = new PaintSnapshot(name);
        
        // Добавляем данные о всех покрашенных стенах
        foreach (var pair in paintedWalls)
        {
            if (wallColors.TryGetValue(pair.Key, out Color color) && 
                wallIntensities.TryGetValue(pair.Key, out float intensity))
            {
                snapshot.AddWall(pair.Key, color, intensity);
            }
        }
        
        // Добавляем снимок в список
        if (savedSnapshots.Count >= maxSnapshots)
        {
            // Удаляем самый старый снимок, если превышен лимит
            savedSnapshots.RemoveAt(0);
        }
        
        savedSnapshots.Add(snapshot);
        currentSnapshotIndex = savedSnapshots.Count - 1;
        activeSnapshot = snapshot;
        
        // Оповещаем об изменении списка снимков
        OnSnapshotsChanged?.Invoke(savedSnapshots, currentSnapshotIndex);
    }
    
    // Загрузка снимка по индексу
    public void LoadSnapshot(int index)
    {
        if (index < 0 || index >= savedSnapshots.Count)
        {
            Debug.LogWarning("Попытка загрузить снимок с неверным индексом: " + index);
            return;
        }
        
        // Получаем снимок
        PaintSnapshot snapshot = savedSnapshots[index];
        
        // Очищаем текущие покрашенные стены
        foreach (var wall in paintedWalls.Values)
        {
            Destroy(wall);
        }
        
        paintedWalls.Clear();
        wallColors.Clear();
        wallIntensities.Clear();
        
        // Применяем данные из снимка
        // Примечание: это упрощенная реализация, которая требует, чтобы плоскости были все еще отслеживаемыми
        ARPlaneManager planeManager = raycastManager.GetComponent<ARPlaneManager>();
        
        if (planeManager == null)
        {
            Debug.LogError("Не удается найти ARPlaneManager для загрузки снимка");
            return;
        }
        
        // Находим все нужные плоскости и применяем к ним сохраненные цвета
        foreach (var wallData in snapshot.paintedWalls)
        {
            TrackableId planeId;
            // Используем правильный способ создания TrackableId
            try
            {
                // Парсим строковый идентификатор плоскости
                string[] parts = wallData.planeId.Split('-');
                if (parts.Length == 2)
                {
                    ulong subId1 = ulong.Parse(parts[0], System.Globalization.NumberStyles.HexNumber);
                    ulong subId2 = ulong.Parse(parts[1], System.Globalization.NumberStyles.HexNumber);
                    planeId = new TrackableId(subId1, subId2);
                }
                else
                {
                    Debug.LogWarning($"Некорректный формат идентификатора плоскости: {wallData.planeId}");
                    continue;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Ошибка при парсинге идентификатора плоскости: {e.Message}");
                continue;
            }
            
            ARPlane plane = planeManager.GetPlane(planeId);
            
            if (plane != null)
            {
                // Создаем новый объект стены
                GameObject wallObject = Instantiate(wallPrefab, plane.transform.position, plane.transform.rotation);
                wallObject.transform.parent = plane.transform;
                
                // Масштабируем объект в соответствии с размерами плоскости
                Bounds planeBounds = plane.GetComponent<MeshRenderer>().bounds;
                wallObject.transform.localScale = new Vector3(
                    planeBounds.size.x,
                    planeBounds.size.y,
                    1.0f
                );
                
                // Настраиваем материал
                MeshRenderer wallRenderer = wallObject.GetComponent<MeshRenderer>();
                wallRenderer.material = new Material(wallMaterial);
                wallRenderer.material.color = new Color(
                    wallData.color.r, 
                    wallData.color.g, 
                    wallData.color.b, 
                    wallData.intensity
                );
                
                // Добавляем в словарь
                paintedWalls.Add(planeId, wallObject);
                wallColors[planeId] = wallData.color;
                wallIntensities[planeId] = wallData.intensity;
            }
        }
        
        // Обновляем текущий снимок
        currentSnapshotIndex = index;
        activeSnapshot = snapshot;
        
        // Оповещаем об изменении текущего снимка
        OnSnapshotsChanged?.Invoke(savedSnapshots, currentSnapshotIndex);
    }
    
    // Получить список всех снимков
    public List<PaintSnapshot> GetSnapshots()
    {
        return savedSnapshots;
    }
    
    // Получить индекс текущего активного снимка
    public int GetCurrentSnapshotIndex()
    {
        return currentSnapshotIndex;
    }
} 