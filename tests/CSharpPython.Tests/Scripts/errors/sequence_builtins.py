sorted([1, 'a'])

# ---
sorted([[1], 1])

# ---
sorted([1], key=5)

# ---
sorted([1], bogus=1)

# ---
list(5)

# ---
list(1, 2)

# ---
tuple(5)

# ---
dict(5)

# ---
dict([1])

# ---
dict([(1, 2, 3)])

# ---
dict(1, 2)

# ---
dict([(1,)])

# ---
dict(['ab', 'cd', 'e'])

# ---
enumerate(5)

# ---
enumerate([1], 'a')

# ---
zip(5)

# ---
zip([1], 5)

# ---
reversed(5)

# ---
any(5)

# ---
all(5)

# ---
range()

# ---
range(1, 2, 3, 4)

# ---
range(0, 5, 0)

# ---
range('a')

# ---
range(1.5)

# ---
str(1, 2)

# ---
repr()

# ---
bool(1, 2)

# ---
print(sep=1)

# ---
print(end=1)

# ---
print(foo=1)

# ---
print(sep=None, end=None)[0]
