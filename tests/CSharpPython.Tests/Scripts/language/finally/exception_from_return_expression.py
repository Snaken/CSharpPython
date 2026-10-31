def f():
    try:
        return 1 // 0
    except ZeroDivisionError:
        return 'z'
    finally:
        print('f')
print(f())
