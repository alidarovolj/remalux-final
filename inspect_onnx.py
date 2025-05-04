#!/usr/bin/env python3
# inspect_onnx.py

import onnx
from onnx import numpy_helper, shape_inference
import onnxruntime as ort
import numpy as np
import sys

def inspect_model(proto_path: str, do_shape_infer: bool = True):
    # Загрузка и (опциональная) корректировка форм
    model = onnx.load(proto_path)
    print(f"Loaded ONNX model: {proto_path}")
    print(f"  IR version: {model.ir_version}")
    print(f"  Producer: {model.producer_name}\n")

    if do_shape_infer:
        model = shape_inference.infer_shapes(model)
        print("Applied shape inference\n")

    graph = model.graph

    # --- Inputs ---
    print("=== INPUTS ===")
    for inp in graph.input:
        # Вход может быть также initializer, фильтруем его
        if any(init.name == inp.name for init in graph.initializer):
            continue
        tt = inp.type.tensor_type
        shape = [d.dim_value for d in tt.shape.dim]
        tpe = onnx.mapping.TENSOR_TYPE_TO_NP_TYPE.get(tt.elem_type, tt.elem_type)
        print(f"  {inp.name}: dtype={tpe}, shape={shape}")
    print()

    # --- Outputs ---
    print("=== OUTPUTS ===")
    for out in graph.output:
        tt = out.type.tensor_type
        shape = [d.dim_value for d in tt.shape.dim]
        tpe = onnx.mapping.TENSOR_TYPE_TO_NP_TYPE.get(tt.elem_type, tt.elem_type)
        print(f"  {out.name}: dtype={tpe}, shape={shape}")
    print()

    # --- Initializers (weights) ---
    print("=== INITIALIZERS ===")
    for init in graph.initializer:
        arr = numpy_helper.to_array(init)
        print(f"  {init.name}: dtype={arr.dtype}, shape={list(arr.shape)}")
    print()

    # --- Nodes ---
    print("=== NODES ===")
    for i, node in enumerate(graph.node):
        print(f"{i:04d}: op_type={node.op_type}")
        print(f"       inputs:  {list(node.input)}")
        print(f"       outputs: {list(node.output)}")
    print(f"\nTotal nodes: {len(graph.node)}\n")

def runtime_test(proto_path: str):
    """
    Простой тестовый прогон через onnxruntime:
    создаём dummy-вход нулями и проверяем, что можно выполнить сессию.
    """
    sess = ort.InferenceSession(proto_path, providers=["CPUExecutionProvider"])
    input_meta = sess.get_inputs()[0]
    out_meta = sess.get_outputs()[0]
    # Собираем случайный батч из нулей
    dummy_input = np.zeros([dim if dim > 0 else 1 for dim in input_meta.shape], dtype=np.float32)
    print(f"Running dummy inference on input `{input_meta.name}` shape {dummy_input.shape}...")
    res = sess.run([out_meta.name], {input_meta.name: dummy_input})
    print(f"  Output `{out_meta.name}` shape: {res[0].shape}")

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python inspect_onnx.py path/to/model.onnx [--no-infer] [--test-run]")
        sys.exit(1)
    
    do_shape_infer = "--no-infer" not in sys.argv
    do_test_run = "--test-run" in sys.argv
    
    model_path = sys.argv[1]
    inspect_model(model_path, do_shape_infer)
    
    if do_test_run:
        print("\n=== RUNTIME TEST ===")
        try:
            runtime_test(model_path)
        except Exception as e:
            print(f"Runtime test failed: {e}") 