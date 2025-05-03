using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.UI;

/// <summary>
/// Управление видимостью AR плоскостей
/// </summary>
public class TogglePlanesVisibility : MonoBehaviour
{
    [SerializeField] private ARPlaneManager planeManager;
    [SerializeField] private Button toggleButton;
    [SerializeField] private Text buttonText;
    
    private bool planesVisible = false;
    
    private void Awake()
    {
        // Автоматически находим компоненты, если они не назначены
        if (planeManager == null)
            planeManager = FindObjectOfType<ARPlaneManager>();
            
        if (toggleButton == null && GetComponent<Button>() != null)
            toggleButton = GetComponent<Button>();
    }
    
    private void Start()
    {
        // Начальное состояние - плоскости видимы
        SetPlanesVisibility(true);
        planesVisible = true;
        
        // Добавляем обработчик нажатия кнопки
        if (toggleButton != null)
        {
            toggleButton.onClick.AddListener(TogglePlanes);
            
            // Обновляем текст кнопки
            UpdateButtonText();
        }
        
        Debug.Log("TogglePlanesVisibility: установлена начальная видимость плоскостей: ВИДИМЫЕ");
    }
    
    /// <summary>
    /// Переключает видимость плоскостей
    /// </summary>
    public void TogglePlanes()
    {
        planesVisible = !planesVisible;
        SetPlanesVisibility(planesVisible);
        UpdateButtonText();
    }
    
    /// <summary>
    /// Устанавливает видимость плоскостей
    /// </summary>
    public void SetPlanesVisibility(bool visible)
    {
        if (planeManager == null) return;
        
        planesVisible = visible;
        
        // Обновляем видимость всех существующих плоскостей
        foreach (var plane in planeManager.trackables)
        {
            // Включаем/выключаем визуализатор
            plane.gameObject.SetActive(visible);
        }
        
        // Также настраиваем обработчик событий для новых плоскостей
        planeManager.planesChanged -= OnPlanesChanged;
        if (visible)
        {
            planeManager.planesChanged += OnPlanesChanged;
        }
        
        Debug.Log($"Видимость AR плоскостей: {(visible ? "включена" : "выключена")}");
    }
    
    private void OnPlanesChanged(ARPlanesChangedEventArgs args)
    {
        // Устанавливаем видимость для новых плоскостей
        foreach (var plane in args.added)
        {
            plane.gameObject.SetActive(planesVisible);
        }
    }
    
    private void UpdateButtonText()
    {
        if (buttonText != null)
        {
            buttonText.text = planesVisible ? "Скрыть плоскости" : "Показать плоскости";
        }
    }
    
    private void OnDestroy()
    {
        if (planeManager != null)
        {
            planeManager.planesChanged -= OnPlanesChanged;
        }
        
        if (toggleButton != null)
        {
            toggleButton.onClick.RemoveListener(TogglePlanes);
        }
    }
} 