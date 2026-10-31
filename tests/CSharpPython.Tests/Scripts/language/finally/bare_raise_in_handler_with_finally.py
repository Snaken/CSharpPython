try:
    try:
        raise ValueError('v')
    except ValueError:
        print('h')
        raise
    finally:
        print('f')
except ValueError as e:
    print('out', e)
