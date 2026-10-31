class A:
    def f(self):
        return 1
try:
    A().f(1)
except TypeError:
    print('TypeError')
try:
    A.f()
except TypeError:
    print('TypeError')
