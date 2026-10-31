def bad(x):
    raise ValueError('nope ' + str(x))
try:
    sorted([1,2], key=bad)
except ValueError as e:
    print('caught', e)
