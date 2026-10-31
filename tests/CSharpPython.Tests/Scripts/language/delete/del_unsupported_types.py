for obj in ((1, 2), 'abc', {1, 2}, 5):
    try:
        del obj[0]
    except TypeError as e:
        print('TypeError', e)
t = (1, 2)
try:
    del t.x
except AttributeError as e:
    print('AttributeError', e)
