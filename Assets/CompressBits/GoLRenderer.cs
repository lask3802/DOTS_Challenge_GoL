using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace LASK.GoL.CompressBits
{
   public class GoLRenderer : MonoBehaviour
    {
        public GoLSimulator simulator;
        public MeshRenderer meshRenderer;
        private ComputeBuffer golData;
        private static JobHandle copyJobHandle;
        //private NativeArray<uint2> gpuDataArray;
        
        private static readonly int GoLData = Shader.PropertyToID("GoLData");
        private static readonly int Width = Shader.PropertyToID("Width");
        private static readonly int Height = Shader.PropertyToID("Height");
        private static readonly int BitWidth = Shader.PropertyToID("BitWidth");
        private static readonly int BitHeight = Shader.PropertyToID("BitHeight");


        private void OnEnable()
        {
            simulator.OnGridChanged += OnGridChanged;
        }

        private void OnGridChanged(uint2 size)
        {
            golData?.Dispose();
            var bufferSize = (int)(size.x*size.y/64);
            golData = new ComputeBuffer(bufferSize, sizeof(ulong), ComputeBufferType.Default,
                ComputeBufferMode.SubUpdates);
        }

        void Update()
        {
           Rendering();
        }

        private void Rendering()
        {
           
            if (simulator.currentImplementation == GoLSimulator.Implementation.FoneESquare
                || simulator.currentImplementation == GoLSimulator.Implementation.Liar
                || simulator.currentImplementation == GoLSimulator.Implementation.LiarWrap
               )
            {
                meshRenderer.material.SetInt(Width, (int)simulator.gridSize.x/8);
                meshRenderer.material.SetInt(Height, (int)simulator.gridSize.x/8);
                meshRenderer.material.SetInt(BitWidth, 8);
                meshRenderer.material.SetInt(BitHeight, 8);
            }
            else
            {
                meshRenderer.material.SetInt(Width, (int)simulator.gridSize.x/64);
                meshRenderer.material.SetInt(Height, (int)simulator.gridSize.y);
                meshRenderer.material.SetInt(BitWidth, 64);
                meshRenderer.material.SetInt(BitHeight, 1);
            }
            
            var bufferSize = (int)(simulator.gridSize.x*simulator.gridSize.y/64);
            if (golData == null || bufferSize != golData.count)
            {
                golData?.Dispose();
                golData = new ComputeBuffer(bufferSize, sizeof(ulong), ComputeBufferType.Default,
                    ComputeBufferMode.SubUpdates);
            }
            
            var gpuDataArray = golData.BeginWrite<ulong>(0, bufferSize);
            var arrayCopyJob = new ArrayCopyJob<ulong>
            {
                source = simulator.Grid.Reinterpret<ulong>(),
                destination = gpuDataArray
            };
            //gpuDataArray.CopyFrom(simulator.Grid.Reinterpret<uint2>());
            copyJobHandle = JobHandle.CombineDependencies(copyJobHandle, arrayCopyJob.Schedule());
            
            meshRenderer.material.SetBuffer(GoLData, golData);
            
        }
        
        private void LateUpdate()
        {
            var bufferSize = (int)(simulator.gridSize.x*simulator.gridSize.y/64);
            copyJobHandle.Complete();
            golData?.EndWrite<uint2>(bufferSize);
        }

        private void OnDisable()
        {
            golData?.Dispose();
            golData = null;
            simulator.OnGridChanged -= OnGridChanged;
        }


        
    }
   
    [BurstCompile]
    public struct ArrayCopyJob<T> : IJob where T : unmanaged
    {
        [ReadOnly] public NativeArray<T> source;
        [WriteOnly][NativeDisableUnsafePtrRestriction] public NativeArray<T> destination;

        [BurstCompile]
        public void Execute()
        {
            destination.CopyFrom(source); 
        }
    }
}