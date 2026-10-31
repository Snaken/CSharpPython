class P:
    def __eq__(self, other):
        if not isinstance(other, P):
            return NotImplemented
        return True
a = P()
print(a == P(), a == 1, a != 1, a == a, 1 == a, a != P())
