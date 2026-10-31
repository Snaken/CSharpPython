READ, WRITE, EXEC = 1, 2, 4
perm = READ | WRITE
print(perm, perm & READ != 0, bool(perm & EXEC), perm & ~WRITE, perm ^ READ)
perm |= EXEC
perm &= ~READ
print(perm, [f for f in (READ, WRITE, EXEC) if perm & f])
mask = 0
for bit in (0, 3, 5):
    mask |= 1 << bit
print(mask, bin(mask) if False else mask >> 3 & 1, [i for i in range(8) if mask >> i & 1])
