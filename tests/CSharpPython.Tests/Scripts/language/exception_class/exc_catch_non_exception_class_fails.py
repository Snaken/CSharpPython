class Plain:
    pass
try:
    try:
        raise ValueError('x')
    except Plain:
        print('no')
except TypeError as e:
    print(e)
