class A:
    def __init__(self):
        object.__setattr__(self, 'x', 1)
    def __delattr__(self, name):
        print('del', name)
        object.__delattr__(self, name)
a = A()
print(a.x)
del a.x
print(hasattr(a, 'x'))
try:
    del a.x
except AttributeError:
    print('AttributeError')
