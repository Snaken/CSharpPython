counter = 1
def reset():
    global counter
    del counter
reset()
try:
    counter
except NameError as e:
    print('NameError', e)
def ghost():
    del undefined_name
try:
    ghost()
except UnboundLocalError:
    print('UnboundLocalError')
