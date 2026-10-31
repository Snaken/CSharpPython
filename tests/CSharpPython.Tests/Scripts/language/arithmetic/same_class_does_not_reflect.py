class A:
    def __add__(self, o):
        return NotImplemented
    def __radd__(self, o):
        return 'radd'
try:
    A() + A()
except TypeError as e:
    print(e)
print(1 + A())
