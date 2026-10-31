def f(n):
    total = 0
    try:
        total = n
        return total
    finally:
        for i in range(3):
            total += i
        print('total', total)
print(f(10))
