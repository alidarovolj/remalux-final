using UnityEditor;
using UnityEngine;

/// <summary>
/// Класс, определяющий структуру меню AR-инструментов
/// </summary>
public static class ARMenuSetup
{
    // Меню для OpenCV for Unity
    [MenuItem("Tools/OpenCV for Unity/Create AR Wall Painting Scene", false, 0)]
    public static void CreateARWallPaintingSceneOpenCV()
    {
        ARWallPaintingSceneCreator.CreateARWallPaintingScene();
    }

    // Основное меню AR
    [MenuItem("AR/Create AR Wall Painting Scene", false, 0)]
    public static void CreateARWallPaintingSceneDirectAR()
    {
        ARWallPaintingSceneCreator.CreateARWallPaintingScene();
    }

    // Добавляем пункт в подменю Tools/AR
    [MenuItem("Tools/AR/Create AR Wall Painting Scene", false, 0)]
    public static void CreateARWallPaintingSceneToolsAR()
    {
        ARWallPaintingSceneCreator.CreateARWallPaintingScene();
    }
}