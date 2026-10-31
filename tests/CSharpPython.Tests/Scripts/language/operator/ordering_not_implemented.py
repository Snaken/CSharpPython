class V:
    def __lt__(self, other):
        return NotImplemented
try:
    V() < V()
except TypeError as e:
    print(e)
