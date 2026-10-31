def kind(v):
    if type(v) == int:
        return 'int'
    elif type(v) == str:
        return 'str'
    return 'other'
print(kind(1), kind('a'), kind(1.5), kind(None))
