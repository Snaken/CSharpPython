class A:
    @property
    def x(self): return 1
    @x.setter
    def x(self, v): print('set', v)
    @x.setter
    def x(self, v): print('set again', v)
A().x = 3
print(A().x)
