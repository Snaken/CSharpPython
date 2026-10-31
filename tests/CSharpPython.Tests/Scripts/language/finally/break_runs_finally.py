for i in range(3):
    try:
        if i == 1:
            break
        print('body', i)
    finally:
        print('f', i)
print('end')
