class A:
    def __add__(self, o):
        return 'A.add'
    def __radd__(self, o):
        return 'A.radd'
class B(A):
    def __add__(self, o):
        return 'B.add'
    def __radd__(self, o):
        return 'B.radd'
print(A() + B(), B() + A(), A() + A(), B() + B())
