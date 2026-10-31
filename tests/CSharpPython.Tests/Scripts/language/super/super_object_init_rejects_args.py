class A:
    def __init__(self, x):
        super().__init__(x)
try:
    A(1)
except TypeError as e:
    print('TypeError')
