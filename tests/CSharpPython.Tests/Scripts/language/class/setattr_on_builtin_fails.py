for v in (1, 'a', [1], None):
    try:
        setattr(v, 'x', 1)
    except AttributeError:
        print('AttributeError')
