class A:
    def __bool__(self):
        return 1
try:
    bool(A())
except TypeError as e:
    print(e)
