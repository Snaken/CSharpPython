class Countdown:
    def __init__(self, n): self.n = n
    def __iter__(self): return iter(range(self.n))
    def __reversed__(self):
        print('reversed called')
        return iter(range(self.n - 1, -1, -1))
c = Countdown(3)
print(list(c), list(reversed(c)))
class L:
    def __reversed__(self): return [3, 2, 1]
print(list(reversed(L())))
