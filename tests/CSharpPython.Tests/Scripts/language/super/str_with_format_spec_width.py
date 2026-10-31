class A:
    def __str__(self):
        return 'ab'
print(format(A(), ''))
print('[{:>5}]'.format(str(A())))
