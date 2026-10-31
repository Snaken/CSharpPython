try:
    assert 1 / 0, 'unreachable'
except ZeroDivisionError as e:
    print('ZeroDivisionError', e)
try:
    assert True, 1 / 0
except ZeroDivisionError:
    print('should not happen')
else:
    print('message not evaluated')
try:
    assert False, undefined_name
except NameError as e:
    print('NameError', e)
