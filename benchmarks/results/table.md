| Script | CPython 3.14.7 | .NET 10 (JIT) | Unity 7 IL2CPP | Unity 6.3 IL2CPP | Unity 6.3 Mono |
| --- | ---: | ---: | ---: | ---: | ---: |
| builtins_stream | 60.1 ms | 70.1 ms (1.2x) | 109.8 ms (1.8x) | 111.6 ms (1.9x) | 347.0 ms (5.8x) |
| classes | 5.5 ms | 12.2 ms (2.2x) | 20.8 ms (3.8x) | 28.4 ms (5.2x) | 68.2 ms (12.4x) |
| collections_ops | 10.6 ms | 24.1 ms (2.3x) | 29.4 ms (2.8x) | 35.0 ms (3.3x) | 79.5 ms (7.5x) |
| dict_ops | 3.0 ms | 5.4 ms (1.8x) | 8.7 ms (2.9x) | 8.7 ms (2.9x) | 25.8 ms (8.6x) |
| fib | 2.0 ms | 4.7 ms (2.4x) | 6.1 ms (3.1x) | 7.8 ms (3.9x) | 20.3 ms (10.3x) |
| generators | 3.3 ms | 8.9 ms (2.7x) | 14.0 ms (4.3x) | 15.1 ms (4.6x) | 43.5 ms (13.2x) |
| list_ops | 4.9 ms | 10.1 ms (2.0x) | 14.4 ms (2.9x) | 14.8 ms (3.0x) | 42.0 ms (8.5x) |
| loop_sum | 16.3 ms | 29.3 ms (1.8x) | 41.8 ms (2.6x) | 46.1 ms (2.8x) | 163.3 ms (10.0x) |
| random_math | 11.5 ms | 11.4 ms (1.0x) | 16.1 ms (1.4x) | 20.5 ms (1.8x) | 66.9 ms (5.8x) |
| strings | 1.8 ms | 2.1 ms (1.1x) | 4.5 ms (2.5x) | 6.7 ms (3.6x) | 13.2 ms (7.1x) |
