def f():
    n = 0
    def g():
        nonlocal n
        n += 1
    try:
        g()
        raise ValueError()
    except ValueError:
        g()
    finally:
        g()
    return n
print(f())
