class A:
    def name(self):
        return 'A'
class B(A):
    def name(self):
        return 'B>' + super().name()
class C(B):
    def name(self):
        return 'C>' + super().name()
print(C().name(), B().name(), A().name())
