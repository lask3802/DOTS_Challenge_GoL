using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace LASK.GoL.CompressBits
{
    [BurstCompile]
    public partial struct NextGenerationJobTrival : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction] [ReadOnly]
        public NativeArray<GoLCells> grid;

        [NativeDisableUnsafePtrRestriction] public NativeArray<ulong> nextGrid;
        [ReadOnly] public uint2 gridSize;

        [BurstCompile]
        public void Execute(int index)
        {
            //Debug.Log($"index: ${index}");

            for (uint i = 0; i < 64; i++)
            {
                var coord = ToAbsoluteCoordinate((uint)index, i);
                var neighbors = CountNeighbors((uint)index, i);
                var cell = GetCell(ref grid, coord.x, coord.y, gridSize);
                var isAlive = (cell == 1) ? (neighbors == 2 || neighbors == 3) : (neighbors == 3);
                SetCell(ref nextGrid, coord.x, coord.y, gridSize, isAlive);
            }
        }

        
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int CountNeighbors(uint index, uint bitIndex)
        {
            var co = ToAbsoluteCoordinate(index, bitIndex);
            return GetCell(ref grid, co.x - 1, co.y - 1, gridSize) +
                   GetCell(ref grid, co.x, co.y - 1, gridSize) +
                   GetCell(ref grid, co.x + 1, co.y - 1, gridSize) +
                   GetCell(ref grid, co.x - 1, co.y, gridSize) +
                   GetCell(ref grid, co.x + 1, co.y, gridSize) +
                   GetCell(ref grid, co.x - 1, co.y + 1, gridSize) +
                   GetCell(ref grid, co.x, co.y + 1, gridSize) +
                   GetCell(ref grid, co.x + 1, co.y + 1, gridSize);
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint2 ToAbsoluteCoordinate(uint index, uint bitIndex)
        {
            return new uint2(index % (gridSize.x / 64) * 64 + bitIndex, index / (gridSize.x / 64));
        }

        [BurstCompile]
        private byte GetCell(ref NativeArray<GoLCells> cells, uint x, uint y, in uint2 dim)
        {
            if (x >= dim.x || y >= dim.y)
                return 0;
            var bitIdx = x % 64;
            var cellIdx = (int)((y * dim.x + x) / 64);
            return (byte)((cells[cellIdx].cells >> (int)bitIdx) & 1);
        }

        [BurstCompile]
        private void SetCell(ref NativeArray<ulong> cells, uint x, uint y, in uint2 dim, bool value)
        {
            if (x >= dim.x || y >= dim.y)
                return;
            var bitIdx = x % 64;
            var cellIdx = (int)((y * dim.x + x) / 64);
            var mask = 1ul << (int)bitIdx;

            ulong trueValue = cells[cellIdx] | mask;
            ulong falseValue = cells[cellIdx] & ~mask;
            cells[cellIdx] = value ? trueValue : falseValue;
        }
    }
}