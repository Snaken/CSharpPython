class A:
    def get(self): return 7
    def put(self, v): print('put', v)
    x = property(get, put)
    ro = property(get)
a = A()
print(a.x, a.ro)
a.x = 1
try:
    a.ro = 2
except AttributeError:
    print('read only')
print(type(A.x).__name__, type(A.ro).__name__)
print(isinstance(A.x, property))
