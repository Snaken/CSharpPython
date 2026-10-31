n = 0
while n < 3:
    n += 1
else:
    print('while done', n)
n = 0
while True:
    n += 1
    if n > 4:
        break
else:
    print('never')
print(n)
while False:
    pass
else:
    print('cond false at start')
