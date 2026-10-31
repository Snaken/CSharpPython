class MyError(ValueError):
    pass
try:
    raise MyError('x')
except ValueError as e:
    print('caught', repr(e))
try:
    raise MyError
except Exception as e:
    print('caught2', repr(e), str(e) == '')
