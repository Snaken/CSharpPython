i = 0
while True:
    try:
        try:
            i += 1
            if i == 2:
                break
        finally:
            print('a', i)
    finally:
        print('b', i)
print('end', i)
