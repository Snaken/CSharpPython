a = [1, 2, 2]
print(sorted({*a, 3}))
print(sorted({*a, *'ab', *[1.5]}, key=str))
print(len({*()}))
print(sorted({*range(3), *range(2, 5)}))
