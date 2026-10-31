class A:
    pass
a = A()
try:
    a[0]
except TypeError as e:
    print(e)
try:
    a[0] = 1
except TypeError as e:
    print(e)
