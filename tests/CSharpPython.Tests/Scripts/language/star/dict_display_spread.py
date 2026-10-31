a = {'x': 1, 'y': 2}
b = {'y': 20, 'z': 30}
print({**a})
print({**a, **b})
print({'y': 0, **a})
print({**a, 'y': 0})
print({'first': 1, **b, 'last': 2})
print({**{}, **{}})
print({**a, 1: 'one', 2.5: 'f'})
c = {**a}
c['x'] = 99
print(a, c)
print(len({**a, **b, 'new': 1}))
