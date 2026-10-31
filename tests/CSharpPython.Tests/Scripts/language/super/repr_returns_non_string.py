class A:
    def __repr__(self):
        return None
try:
    repr(A())
except TypeError as e:
    print(e)
