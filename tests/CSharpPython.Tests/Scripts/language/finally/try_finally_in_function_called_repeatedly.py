def f(x):
    try:
        if x > 1:
            raise ValueError('big')
        return x
    except ValueError:
        return -1
    finally:
        print('f', x)
print(f(1), f(2))
