for i in range(2):
    try:
        try:
            raise ValueError(str(i))
        finally:
            print('f', i)
    except ValueError as e:
        print('c', e)
