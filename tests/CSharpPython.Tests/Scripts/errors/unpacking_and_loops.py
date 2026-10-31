for x in True:
    pass

# ---
a, b = 5

# ---
a, b = None

# ---
a, b = 1.5

# ---
a, b = [1]

# ---
a, b = [1, 2, 3]

# ---
a, b = 'abc'

# ---
a, b = ()

# ---
a, b = {}

# ---
a, b = {1: 2}

# ---
a, b = range(3)

# ---
a, b = range(1)

# ---
(a, b), c = 1, 2

# ---
[a, b] = 1

# ---
a, (b, c) = 1, 2

# ---
a, b, c = 1, 2

# ---
a, b = 1, 2, 3

# ---
for a, b in [(1, 2, 3)]:
    pass

# ---
for a, b in [1]:
    pass

# ---
for a, b in [(1,)]:
    pass

# ---
for a, b in 'xyz':
    pass

# ---
for (a, b), c in [((1, 2), 3, 4)]:
    pass

# ---
x = [1]
x[0][0] = 1

# ---
x = (1,)
x[0] = 1
