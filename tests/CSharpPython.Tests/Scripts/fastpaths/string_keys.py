# string constants carry their hash; they must still find, equal and order like any other string
d = {"alpha": 1, "beta": 2, "": 3}
name = "al" + "pha"
print(d["alpha"], d[name], d["beta"], d[""], d.get("gamma", "none"))
d["gamma"] = 4
d[name] = 10
print(sorted(d.items()))
print("alpha" == name, hash("alpha") == hash(name), hash("") == hash(""[:0]))
s = {"x", "y", "x"}
print(len(s), "x" in s, ("x" + "") in s, "z" in s)
t = {("k", 1): "a", ("k", 2): "b"}
print(t[("k", 1)], t[("k" + "", 2)])
words = "the quick the lazy the end".split()
counts = {}
for w in words:
    counts[w] = counts.get(w, 0) + 1
print(sorted(counts.items()))
print({"b": 1, "a": 2} == {"a": 2, "b": 1}, {"a": 1}.keys() == {"a"})
class K:
    pass
k = K()
k.value = 5
setattr(k, "val" + "ue", 6)
print(k.value, getattr(k, "value"), hasattr(k, "val" + "ue"))
for key in ["alpha", "alpha", name]:
    print(key, d.pop(key, "gone"))
print(sorted(d))
