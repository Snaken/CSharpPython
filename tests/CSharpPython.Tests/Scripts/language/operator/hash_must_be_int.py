class K:
    def __hash__(self):
        return 'x'
try:
    hash(K())
except TypeError as e:
    print(e)
