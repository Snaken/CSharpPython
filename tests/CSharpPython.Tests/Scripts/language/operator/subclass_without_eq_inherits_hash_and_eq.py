class A:
    def __init__(self, n):
        self.n = n
    def __eq__(self, other):
        return self.n == other.n
    def __hash__(self):
        return self.n
class B(A):
    pass
print(B(1) == B(1), hash(B(5)), {B(1): 'x'}[A(1)])
