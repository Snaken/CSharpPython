for i in range(3):
    try:
        if i == 5:
            break
    finally:
        print('f', i)
else:
    print('else after finally')
while True:
    try:
        break
    finally:
        print('fin')
else:
    print('no')
