class A:
    def __call__(self, x):
        return x
try:
    A()()
except TypeError as e:
    print('TypeError 1')
try:
    A()(1, 2)
except TypeError as e:
    print('TypeError 2')
