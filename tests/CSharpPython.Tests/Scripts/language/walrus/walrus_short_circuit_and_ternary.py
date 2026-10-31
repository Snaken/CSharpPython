print((a := 0) or (b := 5), a, b)
print((c := 1) or (d := 6), c)
try:
    d
except NameError:
    print('d unset')
print((e := 3) if (f := 0) else (g := 4), e if False else 0, f, g)
