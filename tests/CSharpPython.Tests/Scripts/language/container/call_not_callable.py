class A:
    pass
try:
    A()()
except TypeError as e:
    print(e)
print(callable(A()))
