def f():
    x = 1
    try:
        return x
    finally:
        x = 2
        print('f', x)
print(f())
