using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

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
        
        public bool isDebug = false;

        public event Action<uint2> OnGridChanged;

        public enum Implementation
        {
            FirstAttempt,
            SecondAttempt,
            FoneE,
            FoneESquare,
            Liar,
            LiarWrap,
        }

        public Implementation currentImplementation = Implementation.LiarWrap;

        // Start is called before the first frame update
        void Start()
        {
            InitializeGridJob().Complete();
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

            switch (currentImplementation)
            {
                case Implementation.FirstAttempt:
                    FirstAttempt();
                    break;
                case Implementation.SecondAttempt:
                    SecondAttempt();
                    break;
                case Implementation.FoneE:
                    FoneE();
                    break;
                case Implementation.FoneESquare:
                    FoneESquare();
                    break;
                case Implementation.Liar:
                    Liar();
                    break;
                case Implementation.LiarWrap:
                    LiarWrap();
                    break;
                
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void LiarWrap()
        {
            var job = new LiarWrapJob
            {
                grid = Tick % 2 == 1 ? gridBack : gridFront,
                nextGrid = Tick % 2 == 1 ? gridFront : gridBack,
                ArrayWidth = (int)gridSize.x / 8,
                ArrayHeight = (int)gridSize.y / 8,
            };
            job.Schedule(gridFront.Length, math.min(1024, (int)gridSize.x)).Complete();
        }

        private void Liar()
        {
            var job = new LiarJob
            {
                grid = Tick % 2 == 1 ? gridBack : gridFront,
                nextGrid = Tick % 2 == 1 ? gridFront : gridBack,
                gridSize = gridSize,
                //batchCnt = 4,
                gridSizeInCompressed = new int2((int)gridSize.x / 8, (int)gridSize.y /8),
            };
            job.Schedule(gridFront.Length, math.min(1024, (int)gridSize.x)).Complete();
        }

        private void FoneESquare()
        {
            var job = new FoneEConway.UpdateGridJobBatchSquare()
            {
                ArrayElementWidth = (int)gridSize.x / 8,
                ArrayElementHeight = (int)gridSize.y / 8,
                ArrayElemetCount = (int)gridSize.x * (int)gridSize.y / 64,
                BaseGrid = Tick % 2 == 1 ? gridBack.Reinterpret<ulong>() : gridFront.Reinterpret<ulong>(),
                NewGrid = Tick % 2 == 1 ? gridFront.Reinterpret<ulong>() : gridBack.Reinterpret<ulong>(),
            };
            job.ScheduleBatch(gridFront.Length, math.min(1024, (int)gridSize.x)).Complete();
        }

        private void OnDestroy()
        {
            gridBack.Dispose();
            gridFront.Dispose();
        }

        public JobHandle ResetGrid(uint newGridSize)
        {
            gridSize = new uint2(newGridSize, newGridSize);
            var j = InitializeGridJob();
            Tick = 0;
            OnGridChanged?.Invoke(gridSize);
            isPaused = false;
            return j;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        JobHandle InitializeGridJob()
        {
            gridBack.Dispose();
            gridFront.Dispose();
            
            
            gridBack = new NativeArray<GoLCells>((int)gridSize.x * (int)gridSize.y / 64, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            gridFront = new NativeArray<GoLCells>((int)gridSize.x * (int)gridSize.y / 64, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            if (!isDebug)
            {
                var rand = Random.CreateFromIndex((uint)UnityEngine.Random.Range(0, int.MaxValue));
                var job = new InitializeGridJob
                {
                    grid = gridBack,
                    rand = rand
                };
                return job.Schedule(gridBack.Length, default);
            }
            else
            {
                SetDebugSquare();
                return default;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void FirstAttempt()
        {
            var job = new FirstAttemptJob()
            {
                grid = Tick % 2 == 1 ? gridBack : gridFront,
                nextGrid = Tick % 2 == 1 ? gridFront : gridBack,
                gridSize = gridSize,
                //batchCnt = 4,
                gridSizeInCompressed = new uint2((uint)gridSize.x / 64, (uint)gridSize.y),
            };
            job.Schedule(gridFront.Length, math.min(1024, (int)gridSize.x)).Complete();
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SecondAttempt()
        {
            var job = new SecondAttemptJob
            {
                grid = Tick % 2 == 1 ? gridBack : gridFront,
                nextGrid = Tick % 2 == 1 ? gridFront : gridBack,
                gridSize = gridSize,
                gridSizeInCompressed = new uint2((uint)gridSize.x / 64, (uint)gridSize.y),
                neighborMarker = new ProfilerMarker("NeighbourCounting"),
                aliveMarker = new ProfilerMarker("AliveCounting")
            };
            job.ScheduleBatch(gridFront.Length, math.min(1024, (int)gridSize.x)).Complete();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void FoneE()
        {
            var job = new FoneEConway.UpdateGridJobBatch
            {
                ArrayElementWidth = (int)gridSize.x / 64,
                ArrayElementHeight = (int)gridSize.y,
                ArrayElemetCount = (int)gridSize.x * (int)gridSize.y / 64,
                BaseGrid = Tick % 2 == 1 ? gridBack.Reinterpret<ulong>() : gridFront.Reinterpret<ulong>(),
                NewGrid = Tick % 2 == 1 ? gridFront.Reinterpret<ulong>() : gridBack.Reinterpret<ulong>(),
            };
            job.ScheduleBatch(gridFront.Length, math.min(1024, (int)gridSize.x)).Complete();
        }


        private void SetDebugSquare()
        {
            for(var i = 0 ; i < gridBack.Length; i++)
                gridBack[i] = new GoLCells { cells =0  };
            gridBack[4+8] = new GoLCells { cells = 0x7| 5<<8 | 7<<16  };
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
    }


    public partial struct GoLCells
    {
        public ulong cells;
    }

    [BurstCompile]
    public partial struct InitializeGridJob : IJobFor
    {
        [WriteOnly] public NativeArray<GoLCells> grid;
        [ReadOnly] public Random rand;

        [BurstCompile]
        public void Execute(int index)
        {
            var nextUInt = rand.NextUInt2();
            var val = (ulong)nextUInt.x | (((ulong)nextUInt.y) << 32);
            grid[index] = new GoLCells
            {
                cells = val
            };
        }
    }

    [BurstCompile]
    public partial struct FirstAttemptJob : IJobParallelFor
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
            const ulong maskNs = 0x7;
            const ulong maskCell = 0x5;
            ulong val = 0;

            var n = index - gridSizeInCompressed.x < 0 ? 0 : grid[index - (int)gridSizeInCompressed.x].cells;
            var s = index + gridSizeInCompressed.x >= grid.Length ? 0 : grid[index + (int)gridSizeInCompressed.x].cells;
            var cell = grid[index].cells;

            //Iterate over second to second last bit of the value
            if (!X86.Popcnt.IsPopcntSupported) return;


            //internal cells in ulong
            for (int i = 1; i < 63; i++)
            {
                //cool low level intrinsics for counting neighbors
                var neighbors = X86.Popcnt.popcnt_u64(n & (maskNs << (i - 1))) +
                                X86.Popcnt.popcnt_u64(s & (maskNs << (i - 1))) +
                                X86.Popcnt.popcnt_u64((maskCell << (i - 1)) & cell);

                ulong mask = 1ul << i;
                bool isSet = (grid[index].cells & mask) > 0;
                ulong result = (isSet ? ((neighbors >> 1) == 1 ? mask : 0) : (neighbors - 3 == 0 ? mask : 0));

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

            //Edge cases
            var rightMostNeighbor = ne + e + se + (ulong)X86.Popcnt.popcnt_u64(n & (maskNs << 62)) +
                                    (ulong)X86.Popcnt.popcnt_u64(s & (maskNs << 62)) +
                                    ((cell & 0x4000000000000000) >> 62);
            var setVal = (grid[index].cells & (1ul << 63)) != 0
                ? rightMostNeighbor == 2 || rightMostNeighbor == 3 ? 1ul << 63 : 0
                : rightMostNeighbor == 3
                    ? 1ul << 63
                    : 0;
            val |= setVal;

            var leftMostNeighbor = nw + w + sw + (ulong)X86.Popcnt.popcnt_u64(n & 3) +
                                   (ulong)X86.Popcnt.popcnt_u64(s & 3) + ((cell & 2) >> 1);
            setVal = (grid[index].cells & 1ul) != 0 ? leftMostNeighbor == 2 || leftMostNeighbor == 3 ? 1ul : 0 :
                leftMostNeighbor == 3 ? 1ul : 0;
            val |= setVal;

            nextGrid[index] = new GoLCells { cells = val };
        }
    }


    //FoneE's implementation
    //https://github.com/OfficialFoneE/DOTS-Conway/blob/e77566e5a7c1835257f08e2e5ae0a2869e46ba03/Assets/Scripts/Conway.Jobs.cs

    [BurstCompile]
    public partial struct FoneEConway
    {
        [BurstCompile]
        public struct UpdateGridJobBatch : IJobParallelForBatch
        {
            public int ArrayElementWidth;
            public int ArrayElementHeight;
            public int ArrayElemetCount;

            [ReadOnly] public NativeArray<ulong> BaseGrid;
            [WriteOnly] public NativeArray<ulong> NewGrid;

            public unsafe void Execute(int startIndex, int count)
            {
                var neighborCounts = stackalloc byte[64];

                int startX = startIndex % ArrayElementWidth;
                int startY = startIndex / ArrayElementWidth;

                int x = startX;
                int y = startY;

                for (int index = 0; index < count; index++)
                {
                    int currentIndex = startIndex + index;

                    ulong baseCells = BaseGrid[currentIndex];

                    bool isLeft = x - 1 >= 0;
                    bool isRight = x + 1 < ArrayElementWidth;
                    bool isTop = y + 1 < ArrayElementHeight;
                    bool isBottom = y - 1 >= 0;

                    var topIndex = currentIndex + ArrayElementWidth;
                    var bottomIndex = currentIndex - ArrayElementWidth;
                    var leftIndex = currentIndex - 1;
                    var rightIndex = currentIndex + 1;

                    var leftTopIndex = topIndex - 1;
                    var rightTopIndex = topIndex + 1;
                    var leftBottomIndex = bottomIndex - 1;
                    var rightBottomIndex = bottomIndex + 1;

                    neighborCounts[0] += GetBitValue(baseCells, 1);

                    // If there are cells to the left of this chunk.
                    if (Hint.Likely(isLeft))
                    {
                        neighborCounts[0] += GetBitValue(BaseGrid[leftIndex], 63);

                        // If there are cells to the bottom left of this chunk
                        if (Hint.Likely(isBottom))
                        {
                            neighborCounts[0] += GetBitValue(BaseGrid[leftBottomIndex], 63);
                        }

                        // If there are cells to the top left of this chunk
                        if (Hint.Likely(isTop))
                        {
                            neighborCounts[0] += GetBitValue(BaseGrid[leftTopIndex], 63);
                        }
                    }

                    neighborCounts[63] += GetBitValue(baseCells, 62);

                    // If there are cells to the right of this chunk.
                    if (Hint.Likely(isRight))
                    {
                        neighborCounts[63] += GetBitValue(BaseGrid[rightIndex], 0);

                        // If there are cells to the bottom right of this chunk
                        if (Hint.Likely(isBottom))
                        {
                            neighborCounts[63] += GetBitValue(BaseGrid[rightBottomIndex], 0);
                        }

                        // If there are cells to the top right of this chunk
                        if (Hint.Likely(isTop))
                        {
                            neighborCounts[63] += GetBitValue(BaseGrid[rightTopIndex], 0);
                        }
                    }

                    // Left and right internal.
                    {
                        for (int i = 1; i < 64 - 1; i += 2)
                        {
                            bool isCenterEnabled = IsBitEnabled(baseCells, i);
                            bool isLeftEnabled = IsBitEnabled(baseCells, i - 1);
                            bool isRightEnabled = IsBitEnabled(baseCells, i + 1);

                            neighborCounts[i - 1] += isCenterEnabled ? (byte)1 : (byte)0;
                            neighborCounts[i + 1] += isCenterEnabled ? (byte)1 : (byte)0;
                            neighborCounts[i] += isLeftEnabled ? (byte)1 : (byte)0;
                            neighborCounts[i] += isRightEnabled ? (byte)1 : (byte)0;
                        }
                    }

                    if (Hint.Likely(isTop))
                    {
                        ulong top = BaseGrid[topIndex];

                        neighborCounts[0] += GetBitValue(top, 1);
                        neighborCounts[63] += GetBitValue(top, 62);

                        // The direct top.
                        for (int i = 0; i < 64; i++)
                        {
                            neighborCounts[i] += ((top >> i) & 1) == 1 ? (byte)1 : (byte)0;
                        }

                        // The diagonals.
                        for (int i = 1; i < 64 - 1; i++)
                        {
                            neighborCounts[i] += ((top >> (i - 1)) & 1) == 1 ? (byte)1 : (byte)0;
                            neighborCounts[i] += ((top >> (i + 1)) & 1) == 1 ? (byte)1 : (byte)0;
                        }
                    }

                    if (Hint.Likely(isBottom))
                    {
                        ulong bottom = BaseGrid[bottomIndex];

                        neighborCounts[0] += GetBitValue(bottom, 1);
                        neighborCounts[63] += GetBitValue(bottom, 62);

                        for (int i = 0; i < 64; i++)
                        {
                            neighborCounts[i] += ((bottom >> i) & 1) == 1 ? (byte)1 : (byte)0;
                        }

                        for (int i = 1; i < 64 - 1; i++)
                        {
                            neighborCounts[i] += ((bottom >> (i - 1)) & 1) == 1 ? (byte)1 : (byte)0;
                            neighborCounts[i] += ((bottom >> (i + 1)) & 1) == 1 ? (byte)1 : (byte)0;
                        }
                    }

                    ulong results = 0;

                    for (int i = 0; i < 64; i++)
                    {
                        bool isCellAlive = IsBitEnabled(baseCells, i);

                        if (isCellAlive)
                        {
                            bool isAlive = neighborCounts[i] == 2 | neighborCounts[i] == 3;

                            results |= (isAlive ? 1UL : 0UL) << i;
                        }
                        else
                        {
                            bool isAlive = neighborCounts[i] == 3;

                            results |= (isAlive ? 1UL : 0UL) << i;
                        }
                    }

                    NewGrid[currentIndex] = results;

                    x++;
                    if (x >= ArrayElementWidth)
                    {
                        y++;
                        x = 0;
                    }

                    UnsafeUtility.MemClear(neighborCounts, UnsafeUtility.SizeOf<byte>() * 64);
                }
            }

            public unsafe void Execute(int index)
            {
                var neighborCounts = stackalloc byte[64];

                ulong baseCells = BaseGrid[index];

                int x = index % ArrayElementWidth;
                int y = index / ArrayElementWidth;

                bool isLeft = x - 1 >= 0;
                bool isRight = x + 1 < ArrayElementWidth;
                bool isTop = y + 1 < ArrayElementHeight;
                bool isBottom = y - 1 >= 0;

                var topIndex = index + ArrayElementWidth;
                var bottomIndex = index - ArrayElementWidth;
                var leftIndex = index - 1;
                var rightIndex = index + 1;

                var leftTopIndex = topIndex - 1;
                var rightTopIndex = topIndex + 1;
                var leftBottomIndex = bottomIndex - 1;
                var rightBottomIndex = bottomIndex + 1;

                neighborCounts[0] += GetBitValue(baseCells, 1);

                // If there are cells to the left of this chunk.
                if (isLeft)
                {
                    neighborCounts[0] += GetBitValue(BaseGrid[leftIndex], 63);

                    // If there are cells to the bottom left of this chunk
                    if (isBottom)
                    {
                        neighborCounts[0] += GetBitValue(BaseGrid[leftBottomIndex], 63);
                    }

                    // If there are cells to the top left of this chunk
                    if (isTop)
                    {
                        neighborCounts[0] += GetBitValue(BaseGrid[leftTopIndex], 63);
                    }
                }

                neighborCounts[63] += GetBitValue(baseCells, 62);

                // If there are cells to the right of this chunk.
                if (isRight)
                {
                    neighborCounts[63] += GetBitValue(BaseGrid[rightIndex], 0);

                    // If there are cells to the bottom right of this chunk
                    if (isBottom)
                    {
                        neighborCounts[63] += GetBitValue(BaseGrid[rightBottomIndex], 0);
                    }

                    // If there are cells to the top right of this chunk
                    if (isTop)
                    {
                        neighborCounts[63] += GetBitValue(BaseGrid[rightTopIndex], 0);
                    }
                }

                // Left and right internal.
                {
                    for (int i = 1; i < 64 - 1; i += 2)
                    {
                        bool isCenterEnabled = IsBitEnabled(baseCells, i);
                        bool isLeftEnabled = IsBitEnabled(baseCells, i - 1);
                        bool isRightEnabled = IsBitEnabled(baseCells, i + 1);

                        neighborCounts[i - 1] += isCenterEnabled ? (byte)1 : (byte)0;
                        neighborCounts[i + 1] += isCenterEnabled ? (byte)1 : (byte)0;
                        neighborCounts[i] += isLeftEnabled ? (byte)1 : (byte)0;
                        neighborCounts[i] += isRightEnabled ? (byte)1 : (byte)0;
                    }
                }

                if (isTop)
                {
                    ulong top = BaseGrid[topIndex];

                    neighborCounts[0] += GetBitValue(top, 1);
                    neighborCounts[63] += GetBitValue(top, 62);

                    // The direct top.
                    for (int i = 0; i < 64; i++)
                    {
                        neighborCounts[i] += ((top >> i) & 1) == 1 ? (byte)1 : (byte)0;
                    }

                    // The diagonals.
                    for (int i = 1; i < 64 - 1; i++)
                    {
                        neighborCounts[i] += ((top >> (i - 1)) & 1) == 1 ? (byte)1 : (byte)0;
                        neighborCounts[i] += ((top >> (i + 1)) & 1) == 1 ? (byte)1 : (byte)0;
                    }
                }

                if (isBottom)
                {
                    ulong bottom = BaseGrid[bottomIndex];

                    neighborCounts[0] += GetBitValue(bottom, 1);
                    neighborCounts[63] += GetBitValue(bottom, 62);

                    for (int i = 0; i < 64; i++)
                    {
                        neighborCounts[i] += ((bottom >> i) & 1) == 1 ? (byte)1 : (byte)0;
                    }

                    for (int i = 1; i < 64 - 1; i++)
                    {
                        neighborCounts[i] += ((bottom >> (i - 1)) & 1) == 1 ? (byte)1 : (byte)0;
                        neighborCounts[i] += ((bottom >> (i + 1)) & 1) == 1 ? (byte)1 : (byte)0;
                    }
                }

                ulong results = 0;

                for (int i = 0; i < 64; i++)
                {
                    bool isCellAlive = IsBitEnabled(baseCells, i);

                    if (isCellAlive)
                    {
                        bool isAlive = neighborCounts[i] == 2 | neighborCounts[i] == 3;

                        results |= (isAlive ? 1UL : 0UL) << i;
                    }
                    else
                    {
                        bool isAlive = neighborCounts[i] == 3;

                        results |= (isAlive ? 1UL : 0UL) << i;
                    }
                }

                NewGrid[index] = results;

                //v128 top = new v128();
                //v128 bottom = new v128();
                //v128 center = new v128();
                //v128 left = new v128();
                //v128 right = new v128();

                //tmp = (input ^ (input >> 8)) & 0x0000ff00;
                //input ^= (tmp ^ (tmp << 8));
                //tmp = (input ^ (input >> 4)) & 0x00f000f0;
                //input ^= (tmp ^ (tmp << 4));
                //tmp = (input ^ (input >> 2)) & 0x0c0c0c0c;
                //input ^= (tmp ^ (tmp << 2));
                //tmp = (input ^ (input >> 1)) & 0x22222222;
                //input ^= (tmp ^ (tmp << 1));

                // Maybe we do 64 x 64 chunks.
                // Than calculate the alive for the bounding chunks?
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool IsBitEnabled(ulong value, int index)
            {
                return ((value >> index) & 1) == 1;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static byte GetBitValue(ulong value, int index)
            {
                return IsBitEnabled(value, index) ? (byte)1 : (byte)0;
            }
        }
    }
}