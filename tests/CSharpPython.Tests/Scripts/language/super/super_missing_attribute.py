class A:
    pass
class B(A):
    def f(self):
        return super().nope()
try:
    B().f()
except AttributeError as e:
    print(e)
