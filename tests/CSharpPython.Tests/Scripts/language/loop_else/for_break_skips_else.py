for i in range(5):
    if i == 3:
        print('found')
        break
else:
    print('not found')
print('after')
for i in range(2):
    pass
else:
    print('no break')
