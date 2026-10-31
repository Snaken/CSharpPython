class A:
    def __hash__(self):
        return 1
class B(A):
    def __eq__(self, other):
        return True
print(hash(A()))
try:
    hash(B())
except TypeError as e:
    print(e)
