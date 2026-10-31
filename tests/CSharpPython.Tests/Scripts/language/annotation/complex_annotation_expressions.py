def f(a: list[int], b: dict[str, int] | None = None, c: 'int' = 1, d: (int, str) = (1, 'a')) -> tuple[int, str]:
    return (a, b, c, d)
print(f([1]))
