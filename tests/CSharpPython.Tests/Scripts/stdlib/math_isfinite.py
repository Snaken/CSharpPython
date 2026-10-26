import math
def t(f):
    try:
        r = f()
        print(type(r).__name__, repr(r))
    except Exception as e:
        print(type(e).__name__)
t(lambda: math.isfinite(1.0))
t(lambda: math.isfinite(float('inf')))
t(lambda: math.isfinite(float('nan')))
t(lambda: math.isfinite(5))
t(lambda: math.isfinite(-float('inf')))
