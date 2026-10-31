class A:
    def __str__(self):
        return 5
try:
    str(A())
except TypeError as e:
    print(e)
