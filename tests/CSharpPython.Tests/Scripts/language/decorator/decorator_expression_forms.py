class Box:
    def __init__(self, n): self.n = n
    def mark(self, fn):
        def w():
            return fn() * self.n
        return w
box = Box(3)
@box.mark
def one(): return 1
print(one())
ds = [lambda f: f, lambda f: (lambda: f() + 1)]
@ds[1]
def zero(): return 0
print(zero())
@(lambda f: (lambda: 'x'))
def y(): return 'y'
print(y())
