using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Утилитарный класс для безопасной загрузки шейдеров на разных платформах
/// Решает проблему отсутствия шейдеров в мобильных билдах
/// </summary>
public static class SafeShaderLoader
{
      // Кэш найденных шейдеров для повторного использования
      private static Dictionary<string, Shader> foundShaders = new Dictionary<string, Shader>();

      // Список приоритетных шейдеров для разных платформ
      private static readonly string[] MobileShaders = new string[]
      {
        "Mobile/Unlit (Supports Lightmap)",
        "Mobile/Diffuse",
        "Mobile/Bumped Diffuse",
        "Mobile/Transparent/Diffuse",
        "Mobile/Transparent/Vertex Color",
        "Universal Render Pipeline/Simple Lit",
        "Universal Render Pipeline/Lit",
        "Universal Render Pipeline/Unlit",
        "Universal Render Pipeline/Simple Lit",
        "Unlit/Transparent",
        "Unlit/Transparent Cutout",
        "Transparent/Diffuse",
        "Transparent/VertexLit",
        "Legacy Shaders/Transparent/Diffuse",
        "Standard",
        "Hidden/Internal-Colored"
      };

      // iOS-специфичные шейдеры
      private static readonly string[] IOSShaders = new string[]
      {
        "Universal Render Pipeline/Mobile/Lit",
        "Universal Render Pipeline/Mobile/Unlit",
        "Mobile/Particles/Additive",
        "Mobile/Particles/Alpha Blended"
      };

      /// <summary>
      /// Возвращает первый доступный шейдер из списка или запасной вариант, чтобы избежать null
      /// </summary>
      /// <param name="defaultShaderName">Имя предпочтительного шейдера</param>
      /// <returns>Найденный шейдер или запасной вариант</returns>
      public static Shader FindShader(string defaultShaderName)
      {
            // Проверяем, не кэширован ли уже результат для этого имени
            if (foundShaders.TryGetValue(defaultShaderName, out Shader cachedShader))
            {
                  return cachedShader;
            }

            // Сначала пробуем найти запрошенный шейдер
            Shader shader = Shader.Find(defaultShaderName);
            if (shader != null)
            {
                  foundShaders[defaultShaderName] = shader;
                  return shader;
            }

            // Иначе перебираем список приоритетных шейдеров
            List<string> shadersToTry = new List<string>(MobileShaders);

            // Добавляем iOS-специфичные шейдеры, если платформа iOS
#if UNITY_IOS
        shadersToTry.AddRange(IOSShaders);
#endif

            foreach (string shaderName in shadersToTry)
            {
                  shader = Shader.Find(shaderName);
                  if (shader != null)
                  {
                        // Кэшируем найденный вариант под запрошенным именем
                        foundShaders[defaultShaderName] = shader;
                        Debug.Log($"SafeShaderLoader: Вместо {defaultShaderName} используем доступный шейдер {shaderName}");
                        return shader;
                  }
            }

            // Крайний случай - возвращаем стандартный шейдер ошибки
            shader = Shader.Find("Hidden/InternalErrorShader");
            if (shader != null)
            {
                  foundShaders[defaultShaderName] = shader;
                  Debug.LogWarning($"SafeShaderLoader: Не найден ни один подходящий шейдер вместо {defaultShaderName}, используем шейдер ошибки");
                  return shader;
            }

            // Если даже шейдер ошибки не найден (очень редкий случай), создаем пустой материал
            Debug.LogError($"SafeShaderLoader: Критическая ошибка - не найден даже шейдер ошибки!");
            return null;
      }

      /// <summary>
      /// Создает материал с безопасным шейдером, который будет работать на текущей платформе
      /// </summary>
      /// <param name="defaultShaderName">Имя предпочтительного шейдера</param>
      /// <returns>Новый материал с подходящим шейдером</returns>
      public static Material CreateMaterial(string defaultShaderName)
      {
            Shader shader = FindShader(defaultShaderName);
            if (shader != null)
            {
                  return new Material(shader);
            }

            // Fallback материал с фиксированным цветом
            Material fallbackMaterial = new Material(Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Sprites/Default"));
            fallbackMaterial.color = Color.magenta; // Видимый цвет ошибки
            return fallbackMaterial;
      }

      /// <summary>
      /// Настраивает прозрачность для материала в зависимости от типа шейдера
      /// </summary>
      /// <param name="material">Материал для настройки</param>
      /// <param name="alpha">Уровень прозрачности (0-1)</param>
      public static void SetupTransparency(Material material, float alpha)
      {
            if (material == null) return;

            try
            {
                  string shaderName = material.shader.name;

                  // Настраиваем прозрачность в зависимости от типа шейдера
                  if (shaderName.Contains("Standard") || shaderName.Contains("Universal"))
                  {
                        // URP или Standard шейдеры
                        material.SetFloat("_Mode", 2); // Transparent mode
                        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        material.SetInt("_ZWrite", 0);
                        material.DisableKeyword("_ALPHATEST_ON");
                        material.EnableKeyword("_ALPHABLEND_ON");
                        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        material.renderQueue = 3000;
                  }
                  else if (shaderName.Contains("Mobile") || shaderName.Contains("Transparent"))
                  {
                        // Мобильные или прозрачные шейдеры
                        material.renderQueue = 3000;
                  }

                  // Устанавливаем прозрачность
                  Color color = material.color;
                  material.color = new Color(color.r, color.g, color.b, alpha);
            }
            catch (System.Exception e)
            {
                  Debug.LogWarning($"SafeShaderLoader: Не удалось настроить прозрачность материала: {e.Message}");
            }
      }
}