| Script | CPython 3.14.7 | .NET 10 (JIT) | Unity 7 IL2CPP | Unity 6.3 IL2CPP | Unity 6.3 Mono |
| --- | ---: | ---: | ---: | ---: | ---: |
| builtins_stream | 61.1 ms | 62.1 ms (1.0x) | 111.3 ms (1.8x) | 112.2 ms (1.8x) | 344.3 ms (5.6x) |
| classes | 5.6 ms | 11.1 ms (2.0x) | 19.9 ms (3.5x) | 25.9 ms (4.6x) | 63.1 ms (11.2x) |
| collections_ops | 10.4 ms | 24.0 ms (2.3x) | 30.7 ms (2.9x) | 34.3 ms (3.3x) | 80.1 ms (7.7x) |
| dict_ops | 3.0 ms | 5.2 ms (1.7x) | 8.9 ms (2.9x) | 8.8 ms (2.9x) | 26.0 ms (8.6x) |
| fib | 1.8 ms | 4.7 ms (2.5x) | 6.3 ms (3.4x) | 7.7 ms (4.2x) | 19.3 ms (10.6x) |
| generators | 3.2 ms | 9.3 ms (2.9x) | 13.8 ms (4.3x) | 14.7 ms (4.5x) | 43.4 ms (13.4x) |
| list_ops | 4.8 ms | 9.5 ms (2.0x) | 14.4 ms (3.0x) | 14.7 ms (3.1x) | 42.2 ms (8.8x) |
| loop_sum | 16.4 ms | 26.8 ms (1.6x) | 42.7 ms (2.6x) | 46.0 ms (2.8x) | 163.8 ms (10.0x) |
| random_math | 11.3 ms | 11.3 ms (1.0x) | 16.2 ms (1.4x) | 19.7 ms (1.7x) | 68.0 ms (6.0x) |
| strings | 1.9 ms | 2.1 ms (1.1x) | 4.5 ms (2.4x) | 6.9 ms (3.7x) | 13.0 ms (6.9x) |
