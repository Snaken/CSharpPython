class Falsy:
    def __bool__(self):
        return False
class Truthy:
    pass
assert Truthy()
try:
    assert Falsy(), 'falsy object'
except AssertionError as e:
    print(e)
for value in (0, 0.0, '', [], {}, (), None, set()):
    try:
        assert value
    except AssertionError:
        print('failed for', repr(value))
