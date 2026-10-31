class Memo:
    def __init__(self):
        self.cache = {}
    def __call__(self, n):
        if n < 2:
            return n
        if n not in self.cache:
            self.cache[n] = self(n - 1) + self(n - 2)
        return self.cache[n]
fib = Memo()
print(fib(30), len(fib.cache))
