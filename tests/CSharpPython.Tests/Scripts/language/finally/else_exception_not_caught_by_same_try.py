try:
    try:
        x = 1
    except ValueError:
        print('inner handler')
    else:
        raise ValueError('from else')
except ValueError as e:
    print('outer', e)
