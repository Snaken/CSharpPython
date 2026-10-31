class A: pass
try:
    getattr(A(), 'nope')
except AttributeError:
    print('AttributeError')
try:
    A().nope
except AttributeError:
    print('AttributeError')
try:
    A.nope
except AttributeError:
    print('AttributeError')
