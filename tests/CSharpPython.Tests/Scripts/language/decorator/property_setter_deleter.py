class T:
    def __init__(self): self._c = 0
    @property
    def c(self):
        return self._c
    @c.setter
    def c(self, value):
        if value < -273:
            raise ValueError('too cold')
        self._c = value
    @c.deleter
    def c(self):
        print('deleting')
        self._c = None
t = T()
t.c = 5
print(t.c)
t.c += 2
print(t.c)
try:
    t.c = -300
except ValueError as e:
    print('ValueError', e)
del t.c
print(t.c)
