assert True
assert 1 == 1, 'never shown'
assert [1], 'non-empty'
try:
    assert False
except AssertionError as e:
    print('AssertionError', repr(str(e)), e.args)
try:
    assert 1 > 2, 'one is not bigger'
except AssertionError as e:
    print('AssertionError', e, e.args)
try:
    assert 0
except Exception as e:
    print(type(e).__name__, isinstance(e, AssertionError))
