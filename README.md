# DOTS Challenge Conway's Game of Life

This repo is for storing Unity DOTS Community Challenge #1, hosted by Turbo Makes Games.

It primarily implements two modes:

- Static Size Grid: Implemented using only Jobs + Burst + MonoBehaviour.
- Dynamic Grid: Implemented using ECS.
---
<!-- TOC -->
* [DOTS Challenge Conway's Game of Life](#dots-challenge-conways-game-of-life)
  * [How to Use](#how-to-use)
  * [Benchmark](#benchmark)
    * [Static Size Grid](#static-size-grid)
    * [Dynamic Size Grid](#dynamic-size-grid)
  * [Implementation Details](#implementation-details)
    * [Static Size Grid](#static-size-grid-1)
    * [Dynamic Size Grid](#dynamic-size-grid-1)
  * [Things I Wanted to Do But Didn't](#things-i-wanted-to-do-but-didnt)
  * [Devlog](#devlog)
    * [Trivial Version](#trivial-version)
    * [First Attempt](#first-attempt)
    * [Optimization-1](#optimization-1)
    * [Visiting Discord for Inspiration from FoneE's Code](#visiting-discord-for-inspiration-from-fonees-code)
    * [LIAR (Life In A Register)](#liar-life-in-a-register)
    * [Rendering Stuff](#rendering-stuff)
    * [Dynamic Size Grid](#dynamic-size-grid-2)
    * [Hiding the Time for GPU Data Copy](#hiding-the-time-for-gpu-data-copy)
<!-- TOC -->

## How to Use

There are 2 scenes which demos dynamic grid size and static grid size approach.

Press S and D can switch between 2 scenes.

For Static size Scene:

- The "grid size" field indicates the length and width of a cell, with the length and width being the same value, defaulting to 1024x1024, and ranging from 64 to 2<sup>20</sup> but it crashed on my PC with about 48000.
- To change the size, you need to click the "ReSimulate" button for it to take effect.
- Implement changes will take effect immediately, but switching between square layout and other layouts may cause simulation errors. If an error is displayed, you can click "ReSimulate".
- Best performance algorithm was Square Layout wrap (LIAR) which is also the default algorithm.


For Dynamic size Scene:
- The "grid size" field indicates the length and width of a cell, with the length and width being the same value, defaulting to 1024x1024 there is no size limit.
- To change the size, you need to click the "ReSimulate" button for it to take effect.

## Benchmark

Hardware Spec: 

- CPU: AMD Ryzen 5900X 12C/24T
- GPU: NVIDIA RTX 3070
### Static Size Grid

Just showing the best performance algorithm.

| Grid Size   | Algorithm | FPS |
|-------------|-----------|-----|
| 4096x4096   | Square Layout wrap (LIAR) | 900 |
| 8192x8192   | Square Layout wrap (LIAR) | 360 |
| 16384x16384 | Square Layout wrap (LIAR) | 100 |
| 21568x21568 | Square Layout wrap (LIAR) | 60  |
| 30976x30976 | Square Layout wrap (LIAR) | 30  |

---

### Dynamic Size Grid


| Grid Size | FPS |
|-----------|-----|
| 4096x4096 | 150 |
| 6300x6300 | 60  |
| 8192x8192 | 43  |
---



## Implementation Details

### Static Size Grid
See [Devlog](#devlog) part for more details.

### Dynamic Size Grid
The method of calculating states for the dynamic size grid inherits the LIAR implementation from the static size grid. Each entity represents the state of an 8x8 cell block. Custom GPU Instancing shaders were used for rendering, with a Material override component to pass cell states into the shader.

The performance is approximately 4 times lower than the static size grid with the same number of cells, possibly because I couldn't ensure the contiguity of neighbor cells in memory.


## Things I Wanted to Do But Didn't
- Performance issues with dynamic size: It might be possible to organize chunks or find other ways to improve memory access efficiency, but I didn't have time to try.
- Recycling/disabling entities that don't need updates: I attempted this, but enabling/disabling or massively deleting/creating entities was too performance-heavy, and I didn't have time to further complete it.
- Correctly resetting the ECS system when switching scenes: Since the static size grid completely uses the GameObject workflow, I encountered problems while trying to switch and reset entities. My workaround was to directly delete the World and recreate it. This might not be the most elegant solution, but it was effective. This taught me that switching scenes with ECS might face more challenges than with GameObjects, requiring attention to many different details.
- Manual SIMD to boost performance: I tried manual SIMD in Jobs, but it reduced performance. I'm still trying to understand where the problem lies. Currently, I suspect it's an issue with the CPU execution ports, but I haven't verified this in detail.

## Devlog
### Trivial Version
I rapidly implemented the first version using NativeArray<bool> and used a Compute Shader to render textures. To reduce overhead, I directly copied the array into a ComputeBuffer for the Compute Shader to perform bit-to-pixel mapping. This version accomplished its tasks well, but due to poor bit utilization, its performance was not high. I immediately started working on a more compact data structure.


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

### LIAR (Life In A Register)
To search for a faster Square Layout algorithm, I found this algorithm https://dotat.at/prog/life/liar2.c.
It can quickly calculate the states of the middle 36 cells with only 33 instructions under Burst, thereby only needing to handle the states of the surrounding 28 cells. This increased speed by 50% after use.

### Rendering Stuff
My original rendering method was to copy all cell states to a ComputeBuffer, and use a ComputeShader to draw on a RenderTexture for rendering. Since the size setting of the cell states and the RenderTexture is 1:1, the ComputeShader is also straightforward.
This method becomes GPU bound when the GPU fill rate is insufficient.

### Dynamic Size Grid

Inspired by the code shared by ITR on Discord, we actually don't need a RenderTexture to draw cell states; we can simply use the uv in the fragment shader to find the correct state of the corresponding cell.

### Hiding the Time for GPU Data Copy
When dealing with a large number of cells (16384*16384), copying to CPU takes about 2ms. Since parallelization to GPU doesn't benefit much, this overhead can still be hidden using Jobs.



