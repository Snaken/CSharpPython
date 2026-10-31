class A:
    def __eq__(self, other):
        return True
a = A()
print(a is a, a is A(), a == A(), a is not A())
