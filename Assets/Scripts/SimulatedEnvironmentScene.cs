using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Компонент для управления симулированным окружением в AR
/// Позволяет создавать и настраивать виртуальные объекты для тестирования AR
/// </summary>
public class SimulatedEnvironmentScene : MonoBehaviour
{
    [Header("Настройки среды")]
    [SerializeField] private bool enableSimulation = true;
    [SerializeField] private bool useDefaultEnvironment = true;
    
    [Header("Объекты окружения")]
    [SerializeField] private List<GameObject> environmentObjects = new List<GameObject>();
    
    [Header("Размеры комнаты")]
    [SerializeField] private Vector3 roomSize = new Vector3(5, 3, 5); // ширина, высота, глубина
    [SerializeField] private Vector3 roomOffset = Vector3.zero;
    
    [Header("Отладка")]
    [SerializeField] private bool showDebugVisuals = true;
    [SerializeField] private Color debugColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
    
    // Стены комнаты
    private GameObject leftWall;
    private GameObject rightWall;
    private GameObject frontWall;
    private GameObject backWall;
    private GameObject floor;
    private GameObject ceiling;
    
    void Start()
    {
        if (enableSimulation && useDefaultEnvironment)
        {
            CreateDefaultEnvironment();
        }
    }
    
    /// <summary>
    /// Создает базовую среду для симуляции (комнату с 4 стенами, полом и потолком)
    /// </summary>
    private void CreateDefaultEnvironment()
    {
        // Создаем родительский объект для всех элементов среды
        GameObject envRoot = new GameObject("SimulatedRoom");
        envRoot.transform.SetParent(transform);
        envRoot.transform.localPosition = roomOffset;
        
        // Размеры стен
        float width = roomSize.x;
        float height = roomSize.y;
        float depth = roomSize.z;
        
        // Создаем 4 стены, пол и потолок
        leftWall = CreateWall("LeftWall", new Vector3(-width/2, 0, 0), Quaternion.Euler(0, 90, 0), new Vector3(depth, height, 0.1f), envRoot.transform);
        rightWall = CreateWall("RightWall", new Vector3(width/2, 0, 0), Quaternion.Euler(0, 90, 0), new Vector3(depth, height, 0.1f), envRoot.transform);
        frontWall = CreateWall("FrontWall", new Vector3(0, 0, depth/2), Quaternion.identity, new Vector3(width, height, 0.1f), envRoot.transform);
        backWall = CreateWall("BackWall", new Vector3(0, 0, -depth/2), Quaternion.identity, new Vector3(width, height, 0.1f), envRoot.transform);
        
        floor = CreateWall("Floor", new Vector3(0, -height/2, 0), Quaternion.Euler(90, 0, 0), new Vector3(width, depth, 0.1f), envRoot.transform);
        ceiling = CreateWall("Ceiling", new Vector3(0, height/2, 0), Quaternion.Euler(90, 0, 0), new Vector3(width, depth, 0.1f), envRoot.transform);
        
        // Добавляем созданные объекты в список
        environmentObjects.Add(leftWall);
        environmentObjects.Add(rightWall);
        environmentObjects.Add(frontWall);
        environmentObjects.Add(backWall);
        environmentObjects.Add(floor);
        environmentObjects.Add(ceiling);
        
        // По умолчанию выключаем рендеринг, если отладка не включена
        SetDebugVisualsActive(showDebugVisuals);
    }
    
    /// <summary>
    /// Создает стену или другую плоскость с заданными параметрами
    /// </summary>
    private GameObject CreateWall(string name, Vector3 position, Quaternion rotation, Vector3 scale, Transform parent)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent);
        wall.transform.localPosition = position;
        wall.transform.localRotation = rotation;
        wall.transform.localScale = scale;
        
        // Настраиваем материал для полупрозрачности
        MeshRenderer renderer = wall.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            Material material = new Material(Shader.Find("Transparent/Diffuse"));
            material.color = debugColor;
            renderer.material = material;
        }
        
        // Отключаем коллайдер, чтобы не мешал AR взаимодействию
        Collider collider = wall.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }
        
        return wall;
    }
    
    /// <summary>
    /// Включает или выключает отображение объектов отладки
    /// </summary>
    public void SetDebugVisualsActive(bool active)
    {
        foreach (var obj in environmentObjects)
        {
            if (obj != null)
            {
                MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.enabled = active;
                }
            }
        }
        
        showDebugVisuals = active;
    }
    
    /// <summary>
    /// Переключает видимость визуальной отладки
    /// </summary>
    public void ToggleDebugVisuals()
    {
        SetDebugVisualsActive(!showDebugVisuals);
    }
    
    /// <summary>
    /// Включает или отключает симуляцию окружения
    /// </summary>
    public void SetSimulationEnabled(bool enabled)
    {
        enableSimulation = enabled;
        
        foreach (var obj in environmentObjects)
        {
            if (obj != null)
            {
                obj.SetActive(enabled && showDebugVisuals);
            }
        }
    }
} 