def fmt(template, *vals):
    return template.format(*vals)
print(fmt('{} and {}', 1, 2))
