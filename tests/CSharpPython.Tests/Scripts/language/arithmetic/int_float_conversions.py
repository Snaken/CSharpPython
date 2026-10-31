class V:
    def __init__(self, n):
        self.n = n
    def __int__(self): return self.n
    def __float__(self): return self.n + 0.5
print(int(V(3)), float(V(3)))
class W:
    def __int__(self): return 'x'
    def __float__(self): return 1
for f in (int, float):
    try:
        f(W())
    except TypeError as e:
        print(e)
