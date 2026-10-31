h = 5381
for ch in 'hello':
    h = ((h << 5) + h + ord(ch)) & 0xFFFFFFFF
print(h)
x = 0b1011 if False else 11
count = 0
while x:
    count += x & 1
    x >>= 1
print(count)
