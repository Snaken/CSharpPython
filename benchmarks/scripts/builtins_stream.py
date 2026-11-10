def main():
    n = 1000000
    assert sum(range(n)) == 499999500000
    assert max(range(n)) == n - 1
    assert min(range(n), key=lambda x: -x) == n - 1
    assert any(x == n - 1 for x in range(n))
    assert all(x >= 0 for x in range(n))
    assert sum(x * 0.5 for x in range(100000)) == 2499975000.0

main()
