class Even:
    def __contains__(self, n):
        return n % 2 == 0
e = Even()
print(2 in e, 3 in e, 3 not in e, 4 not in e)
