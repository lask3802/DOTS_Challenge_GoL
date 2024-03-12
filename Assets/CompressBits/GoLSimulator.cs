using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace LASK.GoL.CompressBits
{
    public class GoLSimulator : MonoBehaviour
    {
        public uint2 gridSize;

        private NativeArray<GoLCells> gridBack;
        private NativeArray<GoLCells> gridFront;

        public NativeArray<GoLCells> Grid => Tick % 2 == 1 ? gridFront : gridBack;

        public uint Tick = 0;
        public float TickTime = 0.5f;

        private float tickTimer = 0;
        public bool isPaused = false;

        public JobHandle jobHandle;

        // Start is called before the first frame update
        void Start()
        {
            gridBack = new NativeArray<GoLCells>((int)gridSize.x * (int)gridSize.y / 64, Allocator.Persistent);
            gridFront = new NativeArray<GoLCells>((int)gridSize.x * (int)gridSize.y / 64, Allocator.Persistent);
            var rand = Random.CreateFromIndex(123);
            //SetDebugSquare();
            for (int i = 0; i < gridBack.Length; i++)
            {
                var nextUInt = rand.NextUInt2();
                
                var val = (ulong)nextUInt.x | (((ulong)nextUInt.y) << 32);
                gridBack[i] = new GoLCells
                {
                    cells = val
                };
            }
        }

        private void SetDebugSquare()
        {
            gridBack[16 * 512 + 8] = new GoLCells { cells = 0x7 };
            gridBack[16 * 513 + 8] = new GoLCells { cells = 0x5 };
            gridBack[16 * 514 + 8] = new GoLCells { cells = 0x7 };
        }

        void SetBit(NativeArray<GoLCells> arr, int2 coord, uint2 gridSize, bool value)
        {
            var idx = coord.y * gridSize.x + coord.x;
            var cell = arr[(int)idx / 64];
            var bit = 1ul << (int)idx % 64;
            if (value)
            {
                cell.cells |= bit;
            }
            else
            {
                cell.cells &= ~bit;
            }

            arr[(int)idx / 64] = cell;
        }

        // Update is called once per frame
        void Update()
        {
            if (isPaused) return;
            // Debug.Log($"Frame: {Time.frameCount}, backSum: {gridBack.Sum(b => b? 1: 0)}, frontSum, {gridFront.Sum(b => b? 1: 0)}");
            tickTimer -= Time.deltaTime;
            if (tickTimer > 0) return;
            tickTimer = TickTime;

            Tick++;
            var job = new NextGenerationJob
            {
                grid = Tick % 2 == 1 ? gridBack : gridFront,
                nextGrid = Tick % 2 == 1 ? gridFront : gridBack,
                gridSize = gridSize,
                gridSizeInCompressed = new uint2((uint)gridSize.x / 64, (uint)gridSize.y)
            };
            //job.ScheduleParallel(gridFront.Length, math.min(1024, (int)gridSize.x),default).Complete();
            job.Schedule(gridFront.Length, (int)gridSize.y).Complete();
        }

        private void OnDestroy()
        {
            gridBack.Dispose();
            gridFront.Dispose();
        }
    }

    public partial struct GoLCells
    {
        public ulong cells;
    }

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
            /*
            if (value)
            {
                cells[cellIdx] |= mask;
            }
            else
            {
                cells[cellIdx] &= ~mask;
            }*/
            ulong trueValue = cells[cellIdx] | mask; // 如果要設置位，則進行 OR 運算。
            ulong falseValue = cells[cellIdx] & ~mask; // 如果要清除位，則進行 AND 運算。
            cells[cellIdx] = value ? trueValue : falseValue;
        }
    }

    [BurstCompile]
    public partial struct NextGenerationJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction] [ReadOnly]
        public NativeArray<GoLCells> grid;

        [WriteOnly] public NativeArray<GoLCells> nextGrid;
        [ReadOnly] public uint2 gridSize;
        [ReadOnly] public uint2 gridSizeInCompressed;

        private bool IsLogIndex(int index)
        {
            return index is >= 16 * 512 + 7 and <= 16 * 512 + 9 ||
                   index is >= 16 * 513 + 7 and <= 16 * 513 + 9 ||
                   index is >= 16 * 514 + 7 and <= 16 * 514 + 9;
        }

        [BurstCompile]
        public void Execute(int index)
        {
            // if (IsLogIndex(index))
            // {
            //     Debug.Log($"index: {index}, n: {n}, s: {s}, ne: {ne}, e: {e}, se: {se}, nw: {nw}, w: {w}, sw: {sw}");
            // }

            const ulong maskNs = 0x7;
            const ulong maskCell = 0x5;
            ulong val = 0;

            var n = index - gridSizeInCompressed.x < 0 ? 0 : grid[index - (int)gridSizeInCompressed.x].cells;
            var s = index + gridSizeInCompressed.x >= grid.Length ? 0 : grid[index + (int)gridSizeInCompressed.x].cells;
            var cell = grid[index].cells;

            //Iterate over second to second last bit of the value
            if (!X86.Popcnt.IsPopcntSupported) return;
            for (int i = 1; i < 63; i++)
            {
                //cool low level intrinsics for counting neighbors
                var neighbors = X86.Popcnt.popcnt_u64(n & (maskNs << (i - 1))) +
                                X86.Popcnt.popcnt_u64(s & (maskNs << (i - 1))) +
                                X86.Popcnt.popcnt_u64((maskCell << (i - 1)) & cell);

                ulong mask = 1ul << i;
                bool isSet = (grid[index].cells & mask) > 0;

                ulong result = (isSet ? ((neighbors >> 1) == 1 ? mask : 0) : (neighbors - 3 == 0 ? mask : 0));
                // if(IsLogIndex(index))
                // {
                //     Debug.Log($"index: {index}, i: {i}, cnt: {neighbors}, isSet: {isSet}, result: {result}");
                // }
                val |= result;
            }

            //for east element pick the left most bit of the value, for west element pick the right most bit of the value
            var ne = index % gridSizeInCompressed.x == gridSizeInCompressed.x - 1 || index - gridSizeInCompressed.x < 0
                ? 0
                : (grid[index - (int)gridSizeInCompressed.x + 1].cells) & 1;
            var nw = index % gridSizeInCompressed.x == 0 || index - gridSizeInCompressed.x < 0
                ? 0
                : (grid[index - (int)gridSizeInCompressed.x - 1].cells & 0x8000000000000000) >> 63;

            var e = index % gridSizeInCompressed.x == gridSizeInCompressed.x - 1 ? 0 : grid[index + 1].cells & 1;
            var w = index % gridSizeInCompressed.x == 0 ? 0 : (grid[index - 1].cells & 0x8000000000000000) >> 63;

            var se = index % gridSizeInCompressed.x == gridSizeInCompressed.x - 1 ||
                     index + gridSizeInCompressed.x >= grid.Length
                ? 0
                : grid[index + (int)gridSizeInCompressed.x + 1].cells & 1;
            var sw = index % gridSizeInCompressed.x == 0 || index + gridSizeInCompressed.x >= grid.Length
                ? 0
                : (grid[index + (int)gridSizeInCompressed.x - 1].cells & 0x8000000000000000) >> 63;

            var rightMostNeighbor = ne + e + se + (ulong)X86.Popcnt.popcnt_u64(n & (maskNs << 62)) +
                                    (ulong)X86.Popcnt.popcnt_u64(s & (maskNs << 62)) +
                                    ((cell & 0x4000000000000000) >> 62);
            var setVal = (grid[index].cells & (1ul << 63)) != 0
                ?
                rightMostNeighbor == 2 || rightMostNeighbor == 3 ? 1ul << 63 : 0
                : rightMostNeighbor == 3
                    ? 1ul << 63
                    : 0;
            val |= setVal;
            // if (IsLogIndex(index))
            // {
            //     Debug.Log($"index: {index}, rightMostNeighbor: {rightMostNeighbor}, val: {val}");
            // }

            var leftMostNeighbor = nw + w + sw + (ulong)X86.Popcnt.popcnt_u64(n & 3) +
                                   (ulong)X86.Popcnt.popcnt_u64(s & 3) + ((cell & 2) >> 1);
            setVal = (grid[index].cells & 1ul) != 0 ? leftMostNeighbor == 2 || leftMostNeighbor == 3 ? 1ul : 0 :
                leftMostNeighbor == 3 ? 1ul : 0;
            val |= setVal;
            // if (IsLogIndex(index))
            // {
            //    Debug.Log($"index: {index}, leftMostNeighbor: {leftMostNeighbor}, val: {val}");
            // }
            nextGrid[index] = new GoLCells { cells = val };
        }
    }
}