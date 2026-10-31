def f():
    try:
        return 'r'
    finally:
        print('f')
print(f())
