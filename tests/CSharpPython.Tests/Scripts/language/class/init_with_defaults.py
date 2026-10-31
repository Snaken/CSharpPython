class V:
    def __init__(self, x=1, y=2):
        self.x = x
        self.y = y
v = V()
w = V(5)
u = V(y=7)
print(v.x, v.y, w.x, w.y, u.x, u.y)
