class A:
    def __init__(self):
        self.a = 1
class B(A):
    def __init__(self):
        r = super().__init__()
        self.r = r
print(B().r)
