class A:
    def __init__(self):
        self.a = 1
        self.b = 2
x = A()
y = A()
x.c = 3
print(hasattr(x, 'c'), hasattr(y, 'c'))
