class K:
    def __eq__(self, other):
        return True
try:
    hash(K())
except TypeError as e:
    print(e)
