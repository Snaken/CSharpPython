class A:
    pass
a = A()
a.x = 1
a.y = 2
del a.x
print(hasattr(a, 'x'), a.y)
try:
    del a.x
except AttributeError as e:
    print('AttributeError', e)
A.z = 5
print(A.z)
del A.z
try:
    A.z
except AttributeError as e:
    print('AttributeError', e)
try:
    del A.nothing
except AttributeError as e:
    print('AttributeError', e)
