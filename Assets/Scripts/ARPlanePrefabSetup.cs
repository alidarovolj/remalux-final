using UnityEngine;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Скрипт для автоматической настройки AR Plane Prefab
/// Устраняет ошибку ArgumentNullException: shader в ARPlaneVisualizer
/// </summary>
[RequireComponent(typeof(ARPlaneManager))]
public class ARPlanePrefabSetup : MonoBehaviour
{
      [SerializeField] private GameObject customPlanePrefab; // Наш кастомный префаб плоскости

      [Tooltip("Если кастомный префаб не указан, будет использован стандартный префаб из библиотеки XR Interaction Toolkit")]
      [SerializeField] private bool useDefaultARPrefabIfMissing = true;

      // Пути к стандартным префабам плоскостей
      private readonly string[] planePrefabPaths = new string[]
      {
        "Prefabs/ARPlaneVisualizer",
        "Samples/XR Interaction Toolkit/3.1.1/AR Starter Assets/Prefabs/AR Feathered Plane",
        "MobileARTemplateAssets/Prefabs/ARFeatheredPlane"
      };

      void Awake()
      {
            ARPlaneManager planeManager = GetComponent<ARPlaneManager>();
            if (planeManager == null) return;

            // Проверяем, установлен ли prefab в ARPlaneManager
            if (planeManager.planePrefab == null)
            {
                  // Если у нас есть кастомный префаб, используем его
                  if (customPlanePrefab != null)
                  {
                        planeManager.planePrefab = customPlanePrefab;
                        Debug.Log("ARPlanePrefabSetup: Установлен кастомный префаб плоскости");
                  }
                  // Иначе ищем стандартный префаб
                  else if (useDefaultARPrefabIfMissing)
                  {
                        GameObject defaultPlanePrefab = FindDefaultPlanePrefab();
                        if (defaultPlanePrefab != null)
                        {
                              planeManager.planePrefab = defaultPlanePrefab;
                              Debug.Log($"ARPlanePrefabSetup: Установлен стандартный префаб плоскости: {defaultPlanePrefab.name}");
                        }
                        else
                        {
                              Debug.LogWarning("ARPlanePrefabSetup: Не удалось найти подходящий префаб плоскости!");
                        }
                  }
            }

            // Проверяем, что в префабе есть необходимые компоненты
            if (planeManager.planePrefab != null)
            {
                  // Проверяем наличие МeshRenderer и MeshFilter
                  if (!planeManager.planePrefab.GetComponentInChildren<MeshRenderer>())
                  {
                        Debug.LogWarning("ARPlanePrefabSetup: В префабе плоскости отсутствует MeshRenderer!");
                  }

                  if (!planeManager.planePrefab.GetComponentInChildren<MeshFilter>())
                  {
                        Debug.LogWarning("ARPlanePrefabSetup: В префабе плоскости отсутствует MeshFilter!");
                  }
            }
      }

      /// <summary>
      /// Ищет стандартный префаб плоскости в проекте
      /// </summary>
      private GameObject FindDefaultPlanePrefab()
      {
            foreach (string path in planePrefabPaths)
            {
                  GameObject prefab = Resources.Load<GameObject>(path);
                  if (prefab != null)
                  {
                        return prefab;
                  }
                  else
                  {
                        // Пробуем загрузить через AssetDatabase
                        prefab = FindPrefabAtPath(path);
                        if (prefab != null)
                        {
                              return prefab;
                        }
                  }
            }

            // Если стандартные префабы не найдены, ищем любой префаб с "Plane" в имени
            GameObject[] allPrefabs = Resources.LoadAll<GameObject>("");
            foreach (GameObject prefab in allPrefabs)
            {
                  if (prefab.name.Contains("Plane") || prefab.name.Contains("plane"))
                  {
                        return prefab;
                  }
            }

            return null;
      }

      /// <summary>
      /// Пытается найти префаб по пути относительно папки Assets
      /// </summary>
      private GameObject FindPrefabAtPath(string relativePath)
      {
#if UNITY_EDITOR
        // В редакторе можно использовать AssetDatabase
        string fullPath = "Assets/" + relativePath + ".prefab";
        return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(fullPath);
#else
            // В билде придется использовать другие методы
            return null;
#endif
      }
}