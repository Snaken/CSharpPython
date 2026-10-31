class CM:
    def __enter__(self):
        print('enter')
    def __exit__(self, t, v, tb):
        print('exit', t)
        return False
def f():
    with CM():
        return 'returned'
print(f())
for i in range(3):
    with CM():
        if i == 0:
            continue
        if i == 1:
            break
        print('unreachable')
print('loop done')
i = 0
while True:
    i += 1
    with CM():
        if i > 1:
            break
print(i)
