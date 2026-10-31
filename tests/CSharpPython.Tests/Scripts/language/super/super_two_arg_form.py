class A:
    def hi(self):
        return 'A'
class B(A):
    def hi(self):
        return 'B' + super(B, self).hi()
print(B().hi())
