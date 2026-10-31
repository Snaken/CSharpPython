class A:
    def f(self):
        A = 5
        return A
print(A().f(), type(A()).__name__)
