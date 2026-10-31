def f(a: int, b: str = 'x', *args: int, c: float = 1.5, **kw: str) -> None:
    print(a, b, args, c, kw)
f(1)
f(1, 'y', 2, 3, c=2.5, d='k')
g = lambda a, b=2: a + b
print(g(1))
