x = ...
print(x, repr(x), type(x).__name__, x is Ellipsis, x == Ellipsis, bool(x))
print(... is ..., ... == 1, ... != None)
print(type(...) is type(Ellipsis), isinstance(..., type(Ellipsis)))
