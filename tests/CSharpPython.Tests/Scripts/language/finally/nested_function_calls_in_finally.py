def g():
    print('g')
def f():
    try:
        return 1
    finally:
        g()
print(f())
