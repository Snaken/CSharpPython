class A:
    @property
    def x(self): return 'A.x'
class B(A):
    pass
class C(A):
    @property
    def x(self): return 'C.x'
class D(A):
    x = 5
print(A().x, B().x, C().x, D().x)
class E(A):
    @property
    def x(self): return super().x + '+E'
print(E().x)
