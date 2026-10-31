def f():
    try:
        try:
            return 1
        finally:
            print('inner')
    finally:
        print('outer')
print(f())
