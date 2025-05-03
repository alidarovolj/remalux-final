using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.XR.CoreUtils;
using System;

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
    
    // Для хранения текстур предпросмотра снимков
    private Dictionary<string, Texture2D> snapshotPreviews = new Dictionary<string, Texture2D>();
    
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
            raycastManager = UnityEngine.Object.FindAnyObjectByType<ARRaycastManager>();
            
        if (xrOrigin == null)
            xrOrigin = UnityEngine.Object.FindAnyObjectByType<XROrigin>();
            
        if (arCamera == null && xrOrigin != null)
            arCamera = xrOrigin.Camera;
            
        if (wallSegmentation == null)
            wallSegmentation = UnityEngine.Object.FindAnyObjectByType<WallSegmentation>();
        
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
        // Проверяем, соответствует ли плоскость результатам сегментации стен
        if (wallSegmentation != null)
        {
            float coverage = wallSegmentation.GetPlaneCoverageByMask(plane);
            if (coverage < 0.3f) // Требуем минимум 30% покрытия маской сегментации
            {
                Debug.Log($"Плоскость {plane.trackableId} не распознана как стена по маске сегментации (покрытие: {coverage:P2})");
                return;
            }
        }
        
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
            MeshRenderer rendererComponent = wallObject.GetComponent<MeshRenderer>();
            rendererComponent.material = new Material(wallMaterial);
            
            // Настраиваем материал для корректной работы с прозрачностью
            SetupMaterialForTransparency(rendererComponent.material);
            
            // Добавляем созданный объект в словарь
            paintedWalls.Add(plane.trackableId, wallObject);
            
            // Устанавливаем начальный цвет и интенсивность
            wallColors[plane.trackableId] = currentColor;
            wallIntensities[plane.trackableId] = brushIntensity;
        }
        
        // Обновляем цвет существующего объекта
        MeshRenderer meshRenderer = wallObject.GetComponent<MeshRenderer>();
        
        // Проверяем, не изменилась ли плоскость
        Bounds meshBounds = plane.GetComponent<MeshFilter>().mesh.bounds;
        wallObject.transform.localScale = new Vector3(
            meshBounds.size.x,
            meshBounds.size.y,
            1.0f
        );
        
        // Получаем локальную позицию точки касания относительно плоскости
        Vector3 localHitPos = plane.transform.InverseTransformPoint(hitPose.position);
        
        // Рассчитываем нормализованные координаты для рисования в диапазоне 0-1
        Vector3 planeCenter = meshBounds.center;
        Vector3 planeSize = meshBounds.size;
        
        float normalizedX = (localHitPos.x - planeCenter.x + planeSize.x/2) / planeSize.x;
        float normalizedY = (localHitPos.y - planeCenter.y + planeSize.y/2) / planeSize.y;
        
        // Применяем новый цвет с учетом кисти и точки касания
        ApplyBrushToTexture(plane.trackableId, normalizedX, normalizedY);
        
        // Сохраняем текущий цвет и интенсивность для каждой стены
        wallColors[plane.trackableId] = new Color(currentColor.r, currentColor.g, currentColor.b);
        wallIntensities[plane.trackableId] = brushIntensity;
    }
    
    // Настройка материала для корректной работы с прозрачностью
    private void SetupMaterialForTransparency(Material material)
    {
        material.SetFloat("_Mode", 2); // Режим прозрачности
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 3000;
    }
    
    // Нанесение цвета на текстуру стены
    private void ApplyBrushToTexture(TrackableId planeId, float normalizedX, float normalizedY)
    {
        if (!paintedWalls.TryGetValue(planeId, out GameObject wallObject))
            return;
            
        MeshRenderer renderer = wallObject.GetComponent<MeshRenderer>();
        if (renderer == null)
            return;
            
        // Получаем размер текстуры
        int textureSize = 512; // Рекомендуемый размер для текстуры покраски
        
        // Проверяем, есть ли у объекта пользовательская текстура для рисования
        Texture2D paintTexture = null;
        
        // Получаем текущую текстуру или создаем новую
        if (renderer.material.mainTexture != null && renderer.material.mainTexture is Texture2D)
        {
            paintTexture = renderer.material.mainTexture as Texture2D;
        }
        else
        {
            paintTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            
            // Заполняем текстуру прозрачным цветом
            Color[] pixels = new Color[textureSize * textureSize];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(1f, 1f, 1f, 0f); // Полностью прозрачный
            }
            paintTexture.SetPixels(pixels);
            paintTexture.Apply();
            
            // Назначаем текстуру материалу
            renderer.material.mainTexture = paintTexture;
        }
        
        // Рассчитываем радиус кисти в пикселях
        int brushRadius = Mathf.RoundToInt(brushSize * textureSize / 2);
        
        // Рассчитываем координаты центра кисти в пикселях
        int centerX = Mathf.RoundToInt(normalizedX * textureSize);
        int centerY = Mathf.RoundToInt(normalizedY * textureSize);
        
        // Создаем цвет кисти
        Color brushColor = new Color(currentColor.r, currentColor.g, currentColor.b, brushIntensity);
        
        // Рисуем кистью на текстуре
        for (int y = centerY - brushRadius; y <= centerY + brushRadius; y++)
        {
            for (int x = centerX - brushRadius; x <= centerX + brushRadius; x++)
            {
                // Пропускаем пиксели за пределами текстуры
                if (x < 0 || x >= textureSize || y < 0 || y >= textureSize)
                    continue;
                    
                // Рассчитываем расстояние от текущего пикселя до центра кисти
                float distance = Mathf.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
                
                // Пропускаем пиксели за пределами радиуса кисти
                if (distance > brushRadius)
                    continue;
                    
                // Рассчитываем интенсивность цвета в зависимости от расстояния до центра
                float intensity = 1f - (distance / brushRadius);
                
                // Получаем текущий цвет пикселя
                Color pixelColor = paintTexture.GetPixel(x, y);
                
                // Смешиваем текущий цвет с цветом кисти
                Color newColor = Color.Lerp(pixelColor, brushColor, intensity * brushIntensity);
                
                // Устанавливаем новый цвет пикселя
                paintTexture.SetPixel(x, y, newColor);
            }
        }
        
        // Применяем изменения
        paintTexture.Apply();
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
        
        // Создаем текстуру предпросмотра (для UI)
        if (arCamera != null)
        {
            // Делаем снимок экрана для превью
            RenderTexture tempRT = new RenderTexture(256, 256, 24);
            RenderTexture prevRT = RenderTexture.active;
            RenderTexture.active = tempRT;
            arCamera.targetTexture = tempRT;
            arCamera.Render();
            
            Texture2D previewTexture = new Texture2D(256, 256, TextureFormat.RGB24, false);
            previewTexture.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
            previewTexture.Apply();
            
            // Возвращаем настройки камеры
            arCamera.targetTexture = null;
            RenderTexture.active = prevRT;
            tempRT.Release();
            
            // Сохраняем текстуру для этого снимка
            if (snapshotPreviews.ContainsKey(snapshot.id))
                snapshotPreviews[snapshot.id] = previewTexture;
            else
                snapshotPreviews.Add(snapshot.id, previewTexture);
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
                Bounds meshBounds = plane.GetComponent<MeshRenderer>().bounds;
                wallObject.transform.localScale = new Vector3(
                    meshBounds.size.x,
                    meshBounds.size.y,
                    1.0f
                );
                
                // Настраиваем материал
                MeshRenderer rendererComponent = wallObject.GetComponent<MeshRenderer>();
                rendererComponent.material = new Material(wallMaterial);
                rendererComponent.material.color = new Color(
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
        if (savedSnapshots == null)
            savedSnapshots = new List<PaintSnapshot>();
            
        return savedSnapshots;
    }
    
    // Получить индекс текущего активного снимка
    public int GetCurrentSnapshotIndex()
    {
        return currentSnapshotIndex;
    }
    
    // Получить текущий размер кисти
    public float GetBrushSize()
    {
        return brushSize;
    }
    
    // Получить текущую интенсивность кисти
    public float GetBrushIntensity()
    {
        return brushIntensity;
    }
    
    // Получить превью-текстуру для снимка по ID
    public Texture2D GetSnapshotPreview(string snapshotId)
    {
        if (snapshotPreviews.TryGetValue(snapshotId, out Texture2D preview))
            return preview;
        
        return null;
    }
} 