class A:
    def __setattr__(self, name, value):
        print('set', name, value)
        object.__setattr__(self, name, value)
a = A()
a.x = 1
a.y = a.x + 1
print(a.x, a.y)
