try:
    assert False, 'x'
except (ValueError, AssertionError) as e:
    print('caught', type(e).__name__)
try:
    try:
        assert False, 'inner'
    except ValueError:
        print('wrong')
except Exception as e:
    print('outer', e)
class MyErr(AssertionError):
    pass
try:
    raise MyErr('mine')
except AssertionError as e:
    print(type(e).__name__, e)
print(issubclass(AssertionError, Exception), AssertionError.__name__)
