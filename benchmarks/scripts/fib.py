def fib(n):
    if n < 2:
        return n
    return fib(n - 1) + fib(n - 2)

def main():
    assert fib(24) == 46368

main()
