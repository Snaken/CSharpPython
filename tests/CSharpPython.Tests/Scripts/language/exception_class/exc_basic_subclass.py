class MyError(Exception):
    pass
try:
    raise MyError('boom')
except MyError as e:
    print(e, type(e).__name__, e.args, isinstance(e, Exception))
