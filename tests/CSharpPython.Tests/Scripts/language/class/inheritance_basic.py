class A:
    def hello(self):
        return 'A'
class B(A):
    pass
print(B().hello(), isinstance(B(), A), isinstance(A(), B))
