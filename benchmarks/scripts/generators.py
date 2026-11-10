def count(n):
    for i in range(n):
        yield i

def main():
    total = 0
    for x in count(200000):
        total += x
    assert total == 19999900000

main()
