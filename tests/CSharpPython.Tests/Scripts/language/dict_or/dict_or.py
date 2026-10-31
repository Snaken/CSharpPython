a = {'x': 1, 'y': 2}
b = {'y': 20, 'z': 30}
c = a | b
print(c, a, b)
print(b | a)
print({} | {}, {1: 1} | {})
d = a
d |= b
print(d is a, a)
e = {'k': 1}
e |= [('m', 2), ('n', 3)]
print(e)
e |= {'q': 9}.items() if False else {'q': 9}
print(e)
print(list((a | b).keys()))
