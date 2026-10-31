class A:
    def __eq__(self, other):
        return True
class B:
    def __eq__(self, other):
        return True
    def __ne__(self, other):
        return 'custom'
print(A() != A(), B() != B())
