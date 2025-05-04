import onnx
from onnx import numpy_helper

def inspect_onnx_model(model_path: str):
    # Загружаем модель
    model = onnx.load(model_path)
    graph = model.graph

    print(f"Model IR version: {model.ir_version}")
    print(f"Producer name: {model.producer_name}\n")

    # Инспекция входов
    print("=== Inputs ===")
    for inp in graph.input:
        name = inp.name
        # Получаем прототип типа (тип и форму)
        tensor_type = inp.type.tensor_type
        elem_type = tensor_type.elem_type
        shape = [dim.dim_value for dim in tensor_type.shape.dim]
        print(f"Name: {name}")
        print(f"  ElemType: {elem_type}")
        print(f"  Shape: {shape}\n")

    # Инспекция выходов
    print("=== Outputs ===")
    for out in graph.output:
        name = out.name
        tensor_type = out.type.tensor_type
        elem_type = tensor_type.elem_type
        shape = [dim.dim_value for dim in tensor_type.shape.dim]
        print(f"Name: {name}")
        print(f"  ElemType: {elem_type}")
        print(f"  Shape: {shape}\n")

    # Инспекция initializers (весов)
    print("=== Initializers (первые 10) ===")
    for i, init in enumerate(graph.initializer):
        if i >= 10:
            print(f"...и еще {len(graph.initializer) - 10} initializers...")
            break
        np_arr = numpy_helper.to_array(init)
        print(f"Name: {init.name}")
        print(f"  Dtype: {np_arr.dtype}")
        print(f"  Shape: {list(np_arr.shape)}")
    print()

    # Инспекция узлов (первые 10)
    print("=== Nodes (первые 10) ===")
    for i, node in enumerate(graph.node):
        if i >= 10:
            print(f"...и еще {len(graph.node) - 10} nodes...")
            break
        print(f"Node {i}: {node.op_type}")
        print(f"  Inputs: {list(node.input)}")
        print(f"  Outputs: {list(node.output)}")
        print()

if __name__ == "__main__":
    import sys
    if len(sys.argv) != 2:
        print("Usage: python inspect_onnx.py path/to/model.onnx")
        print("Using default path: A:/Unity Projects/My project/Assets/Models/model.onnx")
        model_path = "A:/Unity Projects/My project/Assets/Models/model.onnx"
    else:
        model_path = sys.argv[1]
    
    inspect_onnx_model(model_path) 