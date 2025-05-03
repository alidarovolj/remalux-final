using UnityEngine;

#if UNITY_EDITOR
/// <summary>
/// Скрипт для управления симуляционной камерой в редакторе.
/// Позволяет перемещаться и поворачиваться в режиме симуляции AR
/// </summary>
public class EditorCameraController : MonoBehaviour
{
    [Header("Настройки движения")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float lookSpeed = 2f;
    
    private Camera simulationCamera;
    private float rotationX = 0;
    
    private void Start()
    {
        // Используем камеру, к которой прикреплен этот скрипт
        simulationCamera = GetComponent<Camera>();
        
        if (!simulationCamera)
        {
            Debug.LogError("EditorCameraController требует компонент Camera!");
            enabled = false;
            return;
        }
        
        // Скрываем и блокируем курсор для удобной навигации
        Cursor.lockState = CursorLockMode.None;
    }
    
    private void Update()
    {
        // Управление перемещением с помощью WASD/стрелок
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        Vector3 movement = new Vector3(horizontal, 0, vertical) * moveSpeed * Time.deltaTime;
        
        // Перемещение вверх/вниз с помощью Q/E
        if (Input.GetKey(KeyCode.Q))
            movement.y -= moveSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.E))
            movement.y += moveSpeed * Time.deltaTime;
        
        // Применяем перемещение относительно текущей ориентации камеры
        transform.Translate(movement);
        
        // Управление поворотом с помощью мыши (при зажатой правой кнопке)
        if (Input.GetMouseButton(1)) // Правая кнопка мыши зажата
        {
            float mouseX = Input.GetAxis("Mouse X") * lookSpeed;
            float mouseY = Input.GetAxis("Mouse Y") * lookSpeed;
            
            rotationX -= mouseY;
            rotationX = Mathf.Clamp(rotationX, -90f, 90f); // Ограничиваем угол поворота
            
            transform.localRotation = Quaternion.Euler(rotationX, transform.localEulerAngles.y + mouseX, 0);
        }
        
        // Увеличение скорости при нажатии Shift
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            moveSpeed = 10f;
        }
        else
        {
            moveSpeed = 3f;
        }
    }
}
#endif 