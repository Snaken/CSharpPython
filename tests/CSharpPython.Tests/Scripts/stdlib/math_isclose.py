import math
def t(f):
    try:
        r = f()
        print(type(r).__name__, repr(r))
    except Exception as e:
        print(type(e).__name__)
t(lambda: math.isclose(1.0, 1.0))
t(lambda: math.isclose(1.0, 1.0000000001))
t(lambda: math.isclose(1.0, 1.1))
t(lambda: math.isclose(0.0, 1e-10))
t(lambda: math.isclose(0.0, 1e-10, abs_tol=1e-9))
t(lambda: math.isclose(1e10, 1.00000001e10))
t(lambda: math.isclose(1.0, 1.1, rel_tol=0.2))
t(lambda: math.isclose(float('inf'), float('inf')))
t(lambda: math.isclose(float('inf'), 1e308))
t(lambda: math.isclose(float('nan'), float('nan')))
t(lambda: math.isclose(1, 1))
t(lambda: math.isclose(1.0, 1.0, rel_tol=-1))
t(lambda: math.isclose(1.0, 2.0, abs_tol=-1.0))
t(lambda: math.isclose(1.0, 2.0, bogus=1))
t(lambda: math.isclose(-1.0, 1.0))
t(lambda: math.isclose(2, 2.0000000001))
