try:
    try:
        raise KeyError('k')
    except ValueError:
        print('no')
    finally:
        print('f')
except KeyError:
    print('outer')
