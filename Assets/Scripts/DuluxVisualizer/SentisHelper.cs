using UnityEngine;
using Unity.Sentis;

/// <summary>
/// Вспомогательный класс для проверки наличия Unity Sentis
/// </summary>
public static class SentisHelper
{
      /// <summary>
      /// Проверяет доступность Unity Sentis в проекте
      /// </summary>
      /// <returns>true, если Unity Sentis доступен</returns>
      public static bool IsSentisAvailable()
      {
            try
            {
                  // Пробуем создать экземпляр класса из Sentis
                  var executor = WorkerFactory.ValidateBackend(BackendType.CPU);
                  Debug.Log($"Unity Sentis доступен. Поддерживаемые бэкенды: {executor}");
                  return true;
            }
            catch (System.Exception ex)
            {
                  Debug.LogError($"Unity Sentis недоступен или возникла ошибка: {ex.Message}");
                  return false;
            }
      }

      /// <summary>
      /// Получает информацию о доступных бэкендах Sentis
      /// </summary>
      /// <returns>Строка с информацией о доступных бэкендах</returns>
      public static string GetSentisBackendInfo()
      {
            string info = "Unity Sentis Backends:\n";

            info += $"CPU: {WorkerFactory.IsBackendSupported(BackendType.CPU)}\n";
            info += $"GPU Compute: {WorkerFactory.IsBackendSupported(BackendType.GPUCompute)}\n";

            return info;
      }
}