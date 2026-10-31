for i in range(3):
    for j in range(3):
        if j == 1:
            continue
        if i == 1:
            break
        print(i, j)
    else:
        print('inner done', i)
else:
    print('outer done')
