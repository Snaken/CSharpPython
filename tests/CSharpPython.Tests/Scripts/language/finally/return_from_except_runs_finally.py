def f():
    try:
        raise ValueError('x')
    except ValueError:
        return 'h'
    finally:
        print('f')
print(f())
