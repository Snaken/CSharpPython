class V:
    def __init__(self, x):
        self.x = x
        if x > 0:
            return
        self.x = -1
print(V(1).x, V(0).x)
