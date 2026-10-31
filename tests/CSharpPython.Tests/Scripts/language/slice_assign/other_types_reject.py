for target in ((1, 2, 3), 'abc', range(3)):
    try:
        target[0:1] = [1]
    except TypeError as e:
        print('TypeError', e)
