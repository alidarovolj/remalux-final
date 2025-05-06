using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
/// <summary>
/// Инструмент для создания AR стены-префаба
/// </summary>
public class ARWallPrefabCreator : Editor
{
      /// <summary>
      /// Создает префаб AR стены для тестирования
      /// </summary>
      [MenuItem("Tools/AR Wall Painting/Create AR Wall Prefab", false, 30)]
      public static void CreateARWallPrefab()
      {
            // Создаем GameObject для стены
            GameObject wallObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            wallObject.name = "AR Wall";

            // Устанавливаем размер стены
            wallObject.transform.localScale = new Vector3(5, 3, 1);

            // Устанавливаем позицию стены перед камерой
            wallObject.transform.position = new Vector3(0, 1.5f, 2);

            // Устанавливаем поворот стены (к камере)
            wallObject.transform.rotation = Quaternion.Euler(0, 180, 0);

            // Добавляем компонент ARPlane (заглушка для тестирования)
            ARPlaneMockup planeMockup = wallObject.AddComponent<ARPlaneMockup>();

            // Добавляем материал для стены
            Material wallMaterial = new Material(Shader.Find("Standard"));
            wallMaterial.color = new Color(0.9f, 0.9f, 0.9f);
            MeshRenderer renderer = wallObject.GetComponent<MeshRenderer>();
            renderer.material = wallMaterial;

            // Выделяем созданный объект
            Selection.activeGameObject = wallObject;

            // Показываем сообщение об успешном создании
            Debug.Log("AR стена создана успешно. Используйте ее для тестирования окраски.");
            EditorUtility.DisplayDialog("Готово", "AR стена создана успешно. Используйте ее для тестирования окраски.", "OK");
      }
}

/// <summary>
/// Имитация компонента ARPlane для тестирования
/// </summary>
public class ARPlaneMockup : MonoBehaviour
{
      [SerializeField] private bool isWall = true;
      [SerializeField] private float wallConfidence = 0.9f;

      // Методы для имитации ARPlane
      public bool IsWall()
      {
            return isWall;
      }

      public float GetConfidence()
      {
            return wallConfidence;
      }

      // Показывает отладочную информацию в инспекторе
      private void OnDrawGizmos()
      {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(transform.position, transform.localScale);

            // Добавляем текст "AR Wall" над объектом
            Handles.color = Color.cyan;
            Handles.Label(transform.position + Vector3.up * transform.localScale.y * 0.6f, "AR Wall (testing)");
      }
}
#endif