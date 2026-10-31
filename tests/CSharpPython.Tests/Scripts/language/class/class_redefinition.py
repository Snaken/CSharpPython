class A:
    def f(self):
        return 1
a = A()
class A:
    def f(self):
        return 2
print(a.f(), A().f(), type(a) is A)
