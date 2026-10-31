class A:
    def __init__(self):
        self.a = 1
class B(A):
    def __init__(self):
        self.b = 2
b = B()
print(hasattr(b, 'a'), b.b)
