class A:
    pass
for f in (lambda: A() + 1, lambda: 1 + A(), lambda: A() - A(), lambda: A() * 2, lambda: A() / 2, lambda: A() // 2, lambda: A() % 2, lambda: A() ** 2, lambda: -A(), lambda: +A()):
    try:
        f()
    except TypeError as e:
        print(e)
