def main():
    counts = {}
    for i in range(100000):
        key = i % 997
        counts[key] = counts.get(key, 0) + 1
    assert len(counts) == 997
    assert sum(counts.values()) == 100000

main()
