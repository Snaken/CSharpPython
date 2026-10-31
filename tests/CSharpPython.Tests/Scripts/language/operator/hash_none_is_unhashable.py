class K:
    __hash__ = None
try:
    hash(K())
except TypeError as e:
    print(e)
