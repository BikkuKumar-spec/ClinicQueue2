import torch
import transformers
import sys

def check():
    print(f"Python: {sys.version}")
    print(f"Torch: {torch.__version__}")
    print(f"Transformers: {transformers.__version__}")
    try:
        from transformers import AutoModelForSeq2SeqLM, AutoTokenizer
        print("Transformers imports successful.")
    except Exception as e:
        print(f"Transformers import failed: {e}")

if __name__ == "__main__":
    check()
