try:
    try:
        raise ValueError('a')
    finally:
        raise IndexError('b')
except IndexError as e:
    print('caught', e)
except ValueError:
    print('wrong')
