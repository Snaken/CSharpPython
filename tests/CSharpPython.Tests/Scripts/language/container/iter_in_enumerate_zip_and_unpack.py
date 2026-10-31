class Pair:
    def __iter__(self):
        return iter(('x', 'y'))
a, b = Pair()
print(a, b)
print(list(enumerate(Pair())), list(zip(Pair(), [1, 2])), tuple(Pair()))
