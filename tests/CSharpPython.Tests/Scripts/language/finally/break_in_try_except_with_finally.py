for i in range(3):
    try:
        try:
            if i == 1:
                break
        except ValueError:
            pass
    finally:
        print('f', i)
print('end')
