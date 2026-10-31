class A:
    def __init__(self): self._v = 0
    @property
    def v(self): return self._v
    @v.setter
    def v(self, x): self._v = x * 10
    def __setattr__(self, name, value):
        print('hook', name)
        object.__setattr__(self, name, value)
a = A()
a.v = 3
print(a.v, a._v)
