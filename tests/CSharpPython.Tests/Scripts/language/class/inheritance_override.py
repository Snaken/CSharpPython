class A:
    def who(self):
        return 'A'
    def show(self):
        return 'I am ' + self.who()
class B(A):
    def who(self):
        return 'B'
print(A().show(), B().show())
