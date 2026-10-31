class A:
    def __init__(self, x):
        if x < 0:
            raise ValueError('neg')
        self.x = x
try:
    A(-1)
except ValueError as e:
    print(e)
print(A(1).x)
