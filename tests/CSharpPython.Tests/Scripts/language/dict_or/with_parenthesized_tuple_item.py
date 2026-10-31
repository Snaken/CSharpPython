class R:
    def __enter__(self): return 1
    def __exit__(self, *a): return False
try:
    with (R(), R()) as t:
        print(t)
except Exception as e:
    print(type(e).__name__)
