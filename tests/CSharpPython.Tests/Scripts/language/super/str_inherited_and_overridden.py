class A:
    def __str__(self):
        return 'A'
class B(A):
    pass
class C(A):
    def __str__(self):
        return 'C:' + super().__str__()
print(B(), C())
