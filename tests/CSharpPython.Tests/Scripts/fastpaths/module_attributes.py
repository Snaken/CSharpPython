import math
import random
import collections
print(math.pi, math.sqrt(16), math.floor(2.5), random.seed(1), collections.Counter is collections.Counter)
print(type(math).__name__)
f = math.sqrt
print(f(9), [math.sqrt(n) for n in (1, 4, 9)])
for name in ("nothing", "sqroot", "__nope__"):
    try:
        getattr(math, name)
    except AttributeError as e:
        print("AttributeError", e)
try:
    math.nothing
except AttributeError as e:
    print("AttributeError", e)
try:
    math.nothing(1)
except AttributeError as e:
    print("AttributeError", e)
print(hasattr(math, "pi"), hasattr(math, "tau2"), getattr(math, "e", None) is not None)
