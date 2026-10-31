def fib(n):
    return n if n < 2 else fib(n-1) + fib(n-2)
print(sorted([5,3,4], key=fib))
