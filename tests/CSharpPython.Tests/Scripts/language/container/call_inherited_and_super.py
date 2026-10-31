class A:
    def __call__(self, x):
        return 'A' + str(x)
class B(A):
    def __call__(self, x):
        return 'B' + super().__call__(x)
print(B()(1), A()(2))
