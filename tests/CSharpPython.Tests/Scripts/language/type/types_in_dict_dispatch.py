ops = {int: lambda v: v + 1, str: lambda v: v + '!'}
print(ops[type(1)](1), ops[type('a')]('a'))
