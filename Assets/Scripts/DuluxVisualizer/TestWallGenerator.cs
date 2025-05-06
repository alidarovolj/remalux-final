using UnityEngine;
using System.Collections;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Генерирует тестовую стену для отладки в редакторе без AR
/// </summary>
public class TestWallGenerator : MonoBehaviour
{
      [SerializeField] private GameObject wallPrefab;
      [SerializeField] private int numWalls = 1;
      [SerializeField] private float distance = 2.0f;
      [SerializeField] private float width = 2.0f;
      [SerializeField] private float height = 1.5f;

      // Референс на генерируемые стены
      private GameObject[] walls;

      // Флаг для редактора - создавать ли стены автоматически
      [SerializeField] private bool autoCreateWalls = true;

      // Ссылка на камеру, относительно которой создавать стены
      [SerializeField] private Camera targetCamera;

      void Start()
      {
            // Находим камеру, если не указана
            if (targetCamera == null)
            {
                  targetCamera = Camera.main;

                  if (targetCamera == null)
                  {
                        ARCameraManager cameraManager = FindObjectOfType<ARCameraManager>();
                        if (cameraManager != null)
                        {
                              targetCamera = cameraManager.GetComponent<Camera>();
                        }
                  }
            }

            // Автоматически создаем стены при старте
            if (autoCreateWalls)
            {
                  StartCoroutine(CreateWallsDelayed());
            }
      }

      // Отложенное создание стен, чтобы AR система успела инициализироваться
      private IEnumerator CreateWallsDelayed()
      {
            yield return new WaitForSeconds(1.0f);
            CreateTestWalls();
      }

      /// <summary>
      /// Создать тестовые стены в сцене
      /// </summary>
      public void CreateTestWalls()
      {
            // Очищаем старые стены
            DestroyTestWalls();

            if (targetCamera == null)
            {
                  Debug.LogError("Не найдена камера для создания тестовых стен!");
                  return;
            }

            // Создаем массив для стен
            walls = new GameObject[numWalls];

            for (int i = 0; i < numWalls; i++)
            {
                  // Используем префаб, если есть, иначе создаем примитив
                  GameObject wall;
                  if (wallPrefab != null)
                  {
                        wall = Instantiate(wallPrefab, transform);
                  }
                  else
                  {
                        wall = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        wall.transform.parent = transform;
                  }

                  // Задаем размер
                  wall.transform.localScale = new Vector3(width, height, 1);

                  // Позиционируем стену перед камерой
                  wall.transform.position = targetCamera.transform.position + targetCamera.transform.forward * distance;

                  // Поворачиваем стену к камере
                  wall.transform.rotation = Quaternion.LookRotation(-targetCamera.transform.forward, Vector3.up);

                  // Если несколько стен, расставляем их полукругом
                  if (numWalls > 1)
                  {
                        float angle = i * 120f / (numWalls - 1) - 60f;
                        wall.transform.RotateAround(targetCamera.transform.position, Vector3.up, angle);
                  }

                  // Добавляем компонент для имитации AR плоскости
                  ARPlaneMockup planeMockup = wall.AddComponent<ARPlaneMockup>();

                  // Присваиваем имя
                  wall.name = "Test Wall " + i;

                  // Сохраняем в массив
                  walls[i] = wall;
            }
      }

      /// <summary>
      /// Уничтожить все тестовые стены
      /// </summary>
      public void DestroyTestWalls()
      {
            if (walls != null)
            {
                  foreach (GameObject wall in walls)
                  {
                        if (wall != null)
                        {
                              DestroyImmediate(wall);
                        }
                  }
            }
      }

      void OnDrawGizmos()
      {
            // Отображаем в редакторе, где будут созданы стены
            if (targetCamera != null)
            {
                  Gizmos.color = Color.green;
                  for (int i = 0; i < numWalls; i++)
                  {
                        Vector3 position = targetCamera.transform.position + targetCamera.transform.forward * distance;

                        if (numWalls > 1)
                        {
                              float angle = i * 120f / (numWalls - 1) - 60f;
                              Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up);
                              position = targetCamera.transform.position + rotation * (targetCamera.transform.forward * distance);
                        }

                        Gizmos.DrawWireCube(position, new Vector3(width, height, 0.1f));
                  }
            }
      }
}