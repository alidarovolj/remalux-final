using UnityEngine;
using Unity.Barracuda;

/// <summary>
/// Скрипт-заглушка для создания DummyModel для Barracuda
/// </summary>
[CreateAssetMenu(fileName = "DummyModel", menuName = "Machine Learning/Dummy Model")]
public class DummyNNModel : ScriptableObject
{
    public NNModel CreateDummyModel()
    {
        // Получаем ресурс NNModel если он существует
        return Resources.Load<NNModel>("DummyModel");
    }
} 