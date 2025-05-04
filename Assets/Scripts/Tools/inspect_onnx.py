#!/usr/bin/env python3
import onnx
from onnx import numpy_helper

def print_model_info(path):
    model = onnx.load(path)
    onnx.checker.check_model(model)
    print(f"Model IR version: {model.ir_version}")
    print(f"Producer: {model.producer_name}  ({model.producer_version})")
    print(f"Opset imports:")
    for imp in model.opset_import:
        print(f"  domain='{imp.domain or 'ai.onnx'}' opset={imp.version}")
    print()

    print("=== Inputs ===")
    for inp in model.graph.input:
        name = inp.name
        shape = [d.dim_value for d in inp.type.tensor_type.shape.dim]
        dtype = inp.type.tensor_type.elem_type
        print(f"  {name}\tshape={shape}\tdtype={onnx.mapping.TENSOR_TYPE_TO_NP_TYPE[dtype]}")
    print()

    print("=== Outputs ===")
    for out in model.graph.output:
        name = out.name
        shape = [d.dim_value for d in out.type.tensor_type.shape.dim]
        dtype = out.type.tensor_type.elem_type
        print(f"  {name}\tshape={shape}\tdtype={onnx.mapping.TENSOR_TYPE_TO_NP_TYPE[dtype]}")
    print()

    print("=== Initializers (weights) ===")
    for init in model.graph.initializer:
        tensor = numpy_helper.to_array(init)
        print(f"  {init.name}\tshape={tensor.shape}\tdtype={tensor.dtype}")
    print()

    print("=== Nodes ===")
    for node in model.graph.node:
        inputs = ", ".join(node.input)
        outputs = ", ".join(node.output)
        print(f"  {node.op_type:15s}  inputs=[{inputs}]  outputs=[{outputs}]")
    print()

if __name__ == "__main__":
    import sys
    if len(sys.argv) != 2:
        print(f"Usage: {sys.argv[0]} path/to/model.onnx")
        sys.exit(1)
    print_model_info(sys.argv[1]) 