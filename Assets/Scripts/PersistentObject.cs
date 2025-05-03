using UnityEngine;

/// <summary>
/// Компонент, который делает объект постоянным между сценами
/// </summary>
public class PersistentObject : MonoBehaviour
{
    private void Awake()
    {
        // Перед вызовом DontDestroyOnLoad нужно переместить объект в корень иерархии
        transform.SetParent(null);
        
        // Не уничтожать этот объект при загрузке новой сцены
        DontDestroyOnLoad(this.gameObject);
    }
} 