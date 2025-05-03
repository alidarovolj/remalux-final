using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Класс для хранения снимка состояния покраски стен
/// </summary>
[Serializable]
public class PaintSnapshot
{
    // Уникальный идентификатор снимка
    public string id;
    
    // Название снимка
    public string name;
    
    // Дата создания
    public DateTime creationTime;
    
    // Данные о покрашенных стенах
    [Serializable]
    public class WallPaintData
    {
        public string planeId;  // ID плоскости
        public Color color;     // Цвет покраски
        public float intensity; // Интенсивность покраски
    }
    
    // Список данных о покрашенных стенах
    public List<WallPaintData> paintedWalls = new List<WallPaintData>();
    
    // Конструктор для создания нового снимка
    public PaintSnapshot(string snapshotName)
    {
        id = Guid.NewGuid().ToString();
        name = snapshotName;
        creationTime = DateTime.Now;
    }
    
    // Добавление информации о покрашенной стене
    public void AddWall(TrackableId planeId, Color color, float intensity)
    {
        WallPaintData paintData = new WallPaintData
        {
            planeId = planeId.ToString(),
            color = color,
            intensity = intensity
        };
        
        paintedWalls.Add(paintData);
    }
} 