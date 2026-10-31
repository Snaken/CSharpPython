class A:
    def f():
        return 1
try:
    A().f()
except TypeError:
    print('TypeError')
print(A.f())
