class A:
    base = 10
    def f(self, x=base):
        return x
print(A().f(), A().f(1))
