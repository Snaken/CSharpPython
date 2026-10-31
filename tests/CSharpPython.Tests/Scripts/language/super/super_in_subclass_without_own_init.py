class A:
    def __init__(self, v):
        self.v = v
class B(A):
    pass
class C(B):
    def __init__(self):
        super().__init__(9)
print(C().v)
