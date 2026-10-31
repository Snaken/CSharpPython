d = {'a': 1, 'b': 2}
print(d.keys() == {'a', 'b'}, {'a', 'b'} == d.keys(), d.keys() == d.keys(), d.keys() == {'a'}, d.keys() != {'a'})
print(d.keys() == ['a', 'b'], d.keys() == d.copy().keys(), d.keys() == {'b': 0, 'a': 0}.keys())
print(d.items() == {('a', 1), ('b', 2)}, d.items() == d.copy().items(), d.items() == {('a', 1)})
print(d.values() == d.values(), d.values() == d.copy().values(), d.values() == [1, 2])
print(d.keys() <= {'a', 'b', 'c'}, d.keys() < {'a', 'b'}, d.keys() >= {'a'}, d.keys() > {'a'}, {'a'} < d.keys(), {'a', 'b'} <= d.keys())
print(d.keys() < d.copy().keys(), d.keys() <= d.copy().keys(), d.items() <= {('a', 1), ('b', 2), ('c', 3)})
print({'a': 1}.keys().isdisjoint({'b'}) if False else 'skip')
