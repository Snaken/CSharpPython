for i in range(3):
    for j in range(2):
        pass
    else:
        if i == 1:
            break
        print('inner else', i)
else:
    print('outer else')
print('end')
