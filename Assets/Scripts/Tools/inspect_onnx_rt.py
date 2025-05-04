#!/usr/bin/env python3
import numpy as np
import onnxruntime as rt

def inspect_with_ort(path):
    sess = rt.InferenceSession(path, providers=rt.get_available_providers())
    print("Providers:", sess.get_providers())
    print("\n=== Session Inputs ===")
    for inp in sess.get_inputs():
        print(f"  name: {inp.name}")
        print(f"    shape: {inp.shape}")
        print(f"    dtype: {inp.type}")
    print("\n=== Session Outputs ===")
    for out in sess.get_outputs():
        print(f"  name: {out.name}")
        print(f"    shape: {out.shape}")
        print(f"    dtype: {out.type}")
    print()

    # Подготовим фиктивный ввод (заполнение нулями)
    dummy = {inp.name: np.zeros([s if isinstance(s, int) else 1 for s in inp.shape], dtype=np.float32)
             for inp in sess.get_inputs()}

    # Выполним одну итерацию inference
    print("Running dummy inference...")
    res = sess.run(None, dummy)
    for i, out in enumerate(res):
        print(f"  Output[{i}]: shape={out.shape} dtype={out.dtype}")

if __name__ == "__main__":
    import sys
    if len(sys.argv) != 2:
        print(f"Usage: {sys.argv[0]} path/to/model.onnx")
        sys.exit(1)
    inspect_with_ort(sys.argv[1]) 