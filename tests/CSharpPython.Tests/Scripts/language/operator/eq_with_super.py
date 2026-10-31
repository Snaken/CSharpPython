class A:
    def __eq__(self, other):
        return True
class B(A):
    def __eq__(self, other):
        return not super().__eq__(other)
print(B() == B(), A() == B())
