class A:
    def __add__(self, o):
        return NotImplemented
class B:
    def __radd__(self, o):
        return 'B.radd'
print(A() + B(), B() + 1 if False else 'skip')
try:
    A() + A()
except TypeError as e:
    print(e)
try:
    A() + 1
except TypeError as e:
    print(e)
