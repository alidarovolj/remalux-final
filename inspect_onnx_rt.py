#!/usr/bin/env python3
# inspect_onnx_rt.py - версия использующая только onnxruntime без зависимости от onnx

import onnxruntime as ort
import numpy as np
import sys

def inspect_model(proto_path: str):
    """
    Функция для инспекции ONNX модели с использованием только onnxruntime.
    Не требует установки модуля onnx.
    """
    sess = ort.InferenceSession(proto_path, providers=["CPUExecutionProvider"])
    
    print(f"Loaded ONNX model: {proto_path}")
    print(f"OnnxRuntime version: {ort.__version__}\n")
    
    # --- Inputs ---
    print("=== INPUTS ===")
    for inp in sess.get_inputs():
        print(f"  {inp.name}: dtype={inp.type}, shape={inp.shape}")
    print()
    
    # --- Outputs ---
    print("=== OUTPUTS ===")
    for out in sess.get_outputs():
        print(f"  {out.name}: dtype={out.type}, shape={out.shape}")
    print()
    
    # --- Model Metadata ---
    metadata = sess.get_modelmeta()
    if metadata.custom_metadata_map:
        print("=== METADATA ===")
        for key, value in metadata.custom_metadata_map.items():
            print(f"  {key}: {value}")
        print()
    
    print(f"Producer: {metadata.producer_name}")
    print(f"Graph name: {metadata.graph_name}")
    print(f"Domain: {metadata.domain}")
    print(f"Description: {metadata.description}")
    print()
    
    return sess

def runtime_test(sess):
    """
    Простой тестовый прогон через onnxruntime:
    создаём dummy-вход нулями и проверяем, что можно выполнить сессию.
    """
    input_meta = sess.get_inputs()[0]
    out_meta = sess.get_outputs()[0]
    
    # Собираем случайный батч из нулей
    dummy_shape = []
    for dim in input_meta.shape:
        if isinstance(dim, int):
            dummy_shape.append(dim if dim > 0 else 1)
        else:
            # Если размерность задана как строка (динамическая), используем значение по умолчанию
            if dim == 'batch_size':
                dummy_shape.append(1)
            elif dim == 'num_channels':
                dummy_shape.append(3)
            elif dim in ('height', 'width'):
                dummy_shape.append(32)
            else:
                dummy_shape.append(1)
    
    dummy_input = np.zeros(dummy_shape, dtype=np.float32)
    print(f"Running dummy inference on input `{input_meta.name}` shape {dummy_input.shape}...")
    res = sess.run([out_meta.name], {input_meta.name: dummy_input})
    print(f"  Output `{out_meta.name}` shape: {res[0].shape}")
    
    # Показываем статистику по выходному тензору
    output_data = res[0]
    print("\nOutput statistics:")
    print(f"  Min: {output_data.min()}")
    print(f"  Max: {output_data.max()}")
    print(f"  Mean: {output_data.mean()}")
    print(f"  Standard deviation: {output_data.std()}")
    
    # Вывод первых нескольких значений первого канала
    if len(output_data.shape) >= 3:
        print("\nFirst few values of first channel:")
        channel_data = output_data[0, 0]
        print(channel_data.flatten()[:10])
    
    # Анализ вероятностей классов
    if len(output_data.shape) == 4 and output_data.shape[1] > 1:
        # Предполагаем формат [batch, class, height, width]
        print("\nClass probability analysis:")
        
        # Находим среднюю активацию для каждого класса
        class_means = []
        for c in range(output_data.shape[1]):
            mean_activation = output_data[0, c].mean()
            class_means.append((c, mean_activation))
        
        # Сортируем классы по средней активации
        sorted_classes = sorted(class_means, key=lambda x: x[1], reverse=True)
        
        # Выводим топ-5 классов с наибольшей активацией
        print("Top 5 most active classes (potential wall or important structure classes):")
        for i, (class_idx, mean_val) in enumerate(sorted_classes[:5]):
            print(f"  Class {class_idx}: mean activation = {mean_val:.6f}")
        
        # Проверяем класс с индексом 9 (предполагаемый класс стены)
        wall_class_idx = 9
        if wall_class_idx < output_data.shape[1]:
            wall_class_mean = output_data[0, wall_class_idx].mean()
            wall_class_rank = [c for c, _ in sorted_classes].index(wall_class_idx) + 1
            print(f"\nPredefined wall class (index {wall_class_idx}):")
            print(f"  Mean activation: {wall_class_mean:.6f}")
            print(f"  Rank among all classes: {wall_class_rank} out of {output_data.shape[1]}")

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python inspect_onnx_rt.py path/to/model.onnx [--test-run]")
        sys.exit(1)
    
    do_test_run = "--test-run" in sys.argv
    
    model_path = sys.argv[1]
    sess = inspect_model(model_path)
    
    if do_test_run:
        print("\n=== RUNTIME TEST ===")
        try:
            runtime_test(sess)
        except Exception as e:
            print(f"Runtime test failed: {e}") 