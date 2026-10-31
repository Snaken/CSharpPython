class P:
    @property
    def v(self): return 'prop'
p = P()
try:
    p.v = 1
except AttributeError:
    print('read only')
print(p.v)
class W:
    def __init__(self): self._x = 0
    @property
    def x(self): return self._x
    @x.setter
    def x(self, v): self._x = v * 2
w = W()
w.x = 4
print(w.x, w._x)
