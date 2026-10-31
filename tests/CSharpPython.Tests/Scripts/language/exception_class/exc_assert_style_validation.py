class ValidationError(ValueError):
    pass
def check(v):
    if v < 0:
        raise ValidationError('negative: ' + str(v))
    return v
out = []
for v in (1, -2, 3):
    try:
        out.append(check(v))
    except ValueError as e:
        out.append(str(e))
print(out)
