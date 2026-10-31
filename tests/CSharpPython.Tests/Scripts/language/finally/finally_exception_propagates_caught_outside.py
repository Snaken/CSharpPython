try:
    try:
        raise ValueError('x')
    finally:
        print('f')
except ValueError as e:
    print('caught', e)
