try:
    print(5 % 'x')
except TypeError as e:
    print(e)
try:
    print([1] % 2)
except TypeError as e:
    print(e)
print(7 % 3, -7 % 3, 7.5 % 2)
s = 'a%sb'
print(s % 'X', s % 1.5)
print(('%d,' * 3) % (1, 2, 3))
