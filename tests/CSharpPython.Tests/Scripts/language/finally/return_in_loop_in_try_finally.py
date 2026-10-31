def f():
    for i in range(5):
        try:
            if i == 2:
                return i
        finally:
            print('f', i)
print(f())
