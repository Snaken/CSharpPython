class A:
    def __init__(self, x):
        self.x = x
class B(A):
    pass
print(B(4).x)
