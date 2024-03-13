# DOTS Challenge Conway's Game of Life

## Core Concepts
Minimize overhead as much as possible. Given my interest in creating a fixed-size, non-wrapping version initially, I opted to use the JobSystem and BurstCompiler, anticipating that EntityQuery might introduce additional overhead, so Entities have not been utilized yet.

## Trivial Version
I rapidly implemented the first version using NativeArray<bool> and used a Compute Shader to render textures. To reduce overhead, I directly copied the array into a ComputeBuffer for the Compute Shader to perform bit-to-pixel mapping. This version accomplished its tasks well, but due to poor bit utilization, its performance was not high. I immediately started working on a more compact data structure.

## Compressed Bit Version
### First Attempt
This version utilized NativeArray<ulong> to store cell states, with each bit representing a cell. This approach required accessing only 3 values (above, below, and self) for the computation of state for the middle 62 bits, while edge cases required accessing an additional 4 values.

### Optimization-1
Initially, Burst indicated that branches or switches within loops prevented vectorization. I attempted to eliminate branches to allow for better vectorization by Burst, heavily using ternary operators at the cost of readability. However, after examining FoneE's code, I discovered that storing boolean decisions in variables before branching allowed Burst to automatically optimize branches, negating the need for numerous ternary operators.

Moreover, I experimented with using popcnt to calculate the number of neighbors, thinking it would reduce the number of bit shift operations required. This version performed very well, achieving 30 FPS for a 16384x16384 grid on a 5900X.

I initially planned to experiment with different sizes of containers, such as int or two ulongs, to observe any performance differences, so I encapsulated ulong in a struct. However, due to many places hardcoding bit size, this variation has not yet been tested.

### Visiting Discord for Inspiration from FoneE's Code
After completing the first version, I joined a Discord community and shared it, receiving positive feedback. I also reviewed others' implementations. Excited to find that FoneE's approach was similar to mine and also highly efficient, I began studying his code. I found his code to be highly readable and about 30% more efficient under a large number of cell states compared to mine. By comparing the assembly of FoneE's code with my own, I noticed his code was more vectorized. I started researching the differences.

The main change was separating the calculation of neighbors and the determination of aliveness. Previously, the neighbor variable was a value within a loop; separating it required a place to store 64 neighbor variables, for which I referred to FoneE's use of stackalloc to allocate 64 neighbor variables on the stack.

Even though the aliveness determination was already a bit operation, moving it outside the loop also improved performance. Concentrating short, similar logic seems to aid Burst in generating better code.

Additionally, the original edge cases were calculated after the middle 62 bits. Now, needing to access neighbors, I placed edge cases at the beginning and end, allowing the neighbor array to be sequentially written. This move resulted in a significant performance boost, which was quite surprising.

For detailed changes, see FirstAttemptJob and SecondAttemptJob in GoLSimulator.cs for a clearer understanding of the differences.





