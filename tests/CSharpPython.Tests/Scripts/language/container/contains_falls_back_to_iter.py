class R:
    def __iter__(self):
        return iter([1, 2, 3])
print(2 in R(), 5 in R(), 5 not in R())
class N:
    pass
try:
    1 in N()
except TypeError as e:
    print(e)
