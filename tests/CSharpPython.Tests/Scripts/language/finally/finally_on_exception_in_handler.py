try:
    try:
        raise ValueError('a')
    except ValueError:
        raise IndexError('b')
    finally:
        print('f')
except IndexError as e:
    print('caught', e)
