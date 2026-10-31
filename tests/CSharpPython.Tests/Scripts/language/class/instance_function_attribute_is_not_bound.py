class A:
    pass
def hi(x):
    return x
a = A()
a.hi = hi
print(a.hi(5))
