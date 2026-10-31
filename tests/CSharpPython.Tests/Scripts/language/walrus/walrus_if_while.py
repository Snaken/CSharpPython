data = [1, 2, 3, 0, 4]
if (n := len(data)) > 3:
    print('long', n)
i = 0
while (v := data[i]) != 0:
    print('v', v)
    i += 1
print(v)
if (m := 10) > 5 and (k := m * 2) > 15:
    print(m, k)
elif (q := 1):
    print('no', q)
