class A:
    pass
a = A()
def hi(self):
    return 'hi'
A.hi = hi
print(a.hi())
