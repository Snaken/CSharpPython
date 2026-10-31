raise ValueError

# ---
raise TypeError('t')

# ---
raise KeyError('k')

# ---
raise IndexError

# ---
raise Exception('e')

# ---
raise RuntimeError('r')

# ---
raise NotImplementedError

# ---
raise ZeroDivisionError

# ---
raise OverflowError('o')

# ---
raise ArithmeticError

# ---
raise LookupError

# ---
raise StopIteration

# ---
raise MemoryError

# ---
raise NameError('n')

# ---
raise AttributeError('a')

# ---
raise ImportError('i')

# ---
raise RecursionError

# ---
raise UnboundLocalError

# ---
raise 5

# ---
raise None

# ---
raise 'str'

# ---
raise [ValueError]

# ---
raise len

# ---
raise ValueError('a', 'b')

# ---
raise ValueError(1, 2, 3)

# ---
raise ValueError(None)

# ---
raise ValueError(5)

# ---
raise ValueError([1])

# ---
raise ValueError('')

# ---
raise Exception()

# ---
try:
    1 / 0
except 5:
    pass

# ---
try:
    1 / 0
except 'a':
    pass

# ---
try:
    1 / 0
except len:
    pass

# ---
try:
    1 / 0
except (ValueError, 5):
    pass

# ---
try:
    1 / 0
except [ValueError]:
    pass

# ---
try:
    1 / 0
except (5,):
    pass

# ---
try:
    1 / 0
except ((ValueError,), ZeroDivisionError):
    print('nested ok')
    raise OverflowError('after')

# ---
for x in 5:
    pass

# ---
for x in None:
    pass

# ---
for x in 1.5:
    pass
