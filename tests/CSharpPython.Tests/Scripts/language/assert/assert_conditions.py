x = 5
assert x > 1 and x < 10
assert not x == 6
assert x in [1, 5], 'x is missing'
assert isinstance(x, int)
assert (x if x else 0)
assert x == 5, f'x was {x}'
try:
    assert x == 6, f'x was {x}'
except AssertionError as e:
    print(e)
try:
    assert x < 1 or x == 6
except AssertionError as e:
    print('failed', repr(e))
