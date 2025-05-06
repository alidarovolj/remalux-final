using UnityEngine;

namespace DuluxVisualizer
{
      /// <summary>
      /// Улучшенный компонент для применения эффекта окрашивания стен через пост-обработку
      /// </summary>
      [RequireComponent(typeof(Camera))]
      public class ImprovedWallPaintBlit : MonoBehaviour
      {
            [Header("Текстуры")]
            [SerializeField] private RenderTexture _maskTexture;
            [Tooltip("Используется для временного хранения кадра между обновлениями маски")]
            [SerializeField] private RenderTexture _intermediateTexture;

            [Header("Параметры окрашивания")]
            [SerializeField] private Color _paintColor = Color.red;
            [SerializeField, Range(0f, 1f)] private float _opacity = 0.7f;
            [SerializeField, Range(0f, 1f)] private float _preserveShadows = 0.8f;
            [SerializeField, Range(0f, 1f)] private float _smoothEdges = 0.1f;
            [SerializeField] private bool _debugView = false;
            [SerializeField] private bool _useSmoothing = true;
            [SerializeField] private float _smoothingFactor = 0.85f;

            // Ссылки на ресурсы
            private Shader _wallPaintShader;
            private Material _wallPaintMaterial;
            private Camera _camera;

            // Свойства для доступа из других скриптов
            public Color paintColor
            {
                  get { return _paintColor; }
                  set { _paintColor = value; }
            }

            public float opacity
            {
                  get { return _opacity; }
                  set { _opacity = Mathf.Clamp01(value); }
            }

            public float preserveShadows
            {
                  get { return _preserveShadows; }
                  set { _preserveShadows = Mathf.Clamp01(value); }
            }

            public float smoothEdges
            {
                  get { return _smoothEdges; }
                  set { _smoothEdges = Mathf.Clamp01(value); }
            }

            public bool debugView
            {
                  get { return _debugView; }
                  set { _debugView = value; }
            }

            public RenderTexture maskTexture
            {
                  get { return _maskTexture; }
                  set { _maskTexture = value; }
            }

            private void Awake()
            {
                  _camera = GetComponent<Camera>();

                  // Загружаем шейдер
                  _wallPaintShader = Shader.Find("Hidden/ImprovedWallPaint");

                  if (_wallPaintShader == null)
                  {
                        Debug.LogError("ImprovedWallPaint шейдер не найден! Убедитесь, что он находится в папке Shaders и правильно импортирован.");
                        enabled = false;
                        return;
                  }

                  // Создаем материал
                  _wallPaintMaterial = new Material(_wallPaintShader);

                  // Инициализируем промежуточную текстуру, если она не задана
                  if (_useSmoothing && _intermediateTexture == null)
                  {
                        // Создаем промежуточную текстуру с размером экрана
                        _intermediateTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
                        _intermediateTexture.name = "WallPaintIntermediate";
                        _intermediateTexture.filterMode = FilterMode.Bilinear;
                        _intermediateTexture.Create();
                  }
            }

            private void OnRenderImage(RenderTexture source, RenderTexture destination)
            {
                  // Проверяем наличие всех необходимых ресурсов
                  if (_wallPaintMaterial == null || _maskTexture == null)
                  {
                        Graphics.Blit(source, destination);
                        return;
                  }

                  // Настраиваем параметры шейдера
                  _wallPaintMaterial.SetTexture("_MainTex", source);
                  _wallPaintMaterial.SetTexture("_MaskTex", _maskTexture);
                  _wallPaintMaterial.SetColor("_PaintColor", _paintColor);
                  _wallPaintMaterial.SetFloat("_PaintOpacity", _opacity);
                  _wallPaintMaterial.SetFloat("_PreserveShadows", _preserveShadows);
                  _wallPaintMaterial.SetFloat("_SmoothEdges", _smoothEdges);
                  _wallPaintMaterial.SetFloat("_DebugView", _debugView ? 1.0f : 0.0f);

                  // Применяем эффект
                  if (_useSmoothing && _intermediateTexture != null)
                  {
                        // Если используем сглаживание между кадрами
                        if (_intermediateTexture.IsCreated())
                        {
                              // 1. Рендерим текущий кадр
                              Graphics.Blit(source, _intermediateTexture, _wallPaintMaterial);

                              // 2. Смешиваем с предыдущим кадром для временной стабильности
                              ApplyTemporalSmoothing(source, _intermediateTexture, destination);
                        }
                        else
                        {
                              // Если промежуточная текстура не создана, применяем эффект напрямую
                              Graphics.Blit(source, destination, _wallPaintMaterial);
                        }
                  }
                  else
                  {
                        // Без сглаживания просто применяем эффект
                        Graphics.Blit(source, destination, _wallPaintMaterial);
                  }
            }

            /// <summary>
            /// Применяет временное сглаживание между кадрами для уменьшения мерцания
            /// </summary>
            private void ApplyTemporalSmoothing(RenderTexture source, RenderTexture current, RenderTexture destination)
            {
                  // Создаем параметры для смешивания
                  Material blendMat = new Material(Shader.Find("Hidden/Unlit/Transparent"));
                  blendMat.SetTexture("_MainTex", current);
                  blendMat.SetFloat("_Alpha", 1.0f - _smoothingFactor);

                  // Смешиваем текущий кадр с предыдущим
                  Graphics.Blit(current, destination, blendMat);

                  // Очищаем временный материал
                  Destroy(blendMat);
            }

            /// <summary>
            /// Обновляет параметры окрашивания
            /// </summary>
            public void UpdatePaintParameters(Color color, float opacity, float preserveShadows, float smoothEdges)
            {
                  _paintColor = color;
                  _opacity = Mathf.Clamp01(opacity);
                  _preserveShadows = Mathf.Clamp01(preserveShadows);
                  _smoothEdges = Mathf.Clamp01(smoothEdges);
            }

            /// <summary>
            /// Устанавливает режим отладки
            /// </summary>
            public void SetDebugMode(bool enabled)
            {
                  _debugView = enabled;
            }

            /// <summary>
            /// Устанавливает использование временного сглаживания
            /// </summary>
            public void SetSmoothingEnabled(bool enabled, float factor = 0.85f)
            {
                  _useSmoothing = enabled;
                  _smoothingFactor = Mathf.Clamp01(factor);

                  // Создаем или удаляем промежуточную текстуру при необходимости
                  if (_useSmoothing && _intermediateTexture == null)
                  {
                        _intermediateTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
                        _intermediateTexture.name = "WallPaintIntermediate";
                        _intermediateTexture.filterMode = FilterMode.Bilinear;
                        _intermediateTexture.Create();
                  }
                  else if (!_useSmoothing && _intermediateTexture != null)
                  {
                        _intermediateTexture.Release();
                        Destroy(_intermediateTexture);
                        _intermediateTexture = null;
                  }
            }

            private void OnDestroy()
            {
                  // Освобождаем ресурсы
                  if (_wallPaintMaterial != null)
                  {
                        if (Application.isPlaying)
                              Destroy(_wallPaintMaterial);
                        else
                              DestroyImmediate(_wallPaintMaterial);
                  }

                  if (_intermediateTexture != null)
                  {
                        _intermediateTexture.Release();
                        if (Application.isPlaying)
                              Destroy(_intermediateTexture);
                        else
                              DestroyImmediate(_intermediateTexture);
                  }
            }

            private void OnDisable()
            {
                  // Освобождаем ресурсы промежуточной текстуры при отключении
                  if (_intermediateTexture != null)
                  {
                        _intermediateTexture.Release();
                  }
            }
      }
}