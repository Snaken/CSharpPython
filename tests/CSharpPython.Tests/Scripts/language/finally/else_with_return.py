def f():
    try:
        x = 1
    except ValueError:
        return 'h'
    else:
        return 'e'
    finally:
        print('f')
print(f())
