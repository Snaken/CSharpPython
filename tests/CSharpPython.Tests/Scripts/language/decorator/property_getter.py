class C:
    def __init__(self): self._v = 3
    @property
    def v(self):
        return self._v * 2
c = C()
print(c.v)
c._v = 10
print(c.v)
try:
    c.v = 1
except AttributeError as e:
    print('AttributeError')
try:
    del c.v
except AttributeError as e:
    print('AttributeError')
