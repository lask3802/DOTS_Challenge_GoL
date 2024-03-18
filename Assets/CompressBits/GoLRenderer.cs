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
        /*public ComputeShader shader;
        public ComputeShader squareRenderingShader;
        public RenderTexture renderTexture;*/
        public Shader horizontalLayoutShader;
        public Shader squareLayoutShader;
        public MeshRenderer meshRenderer;
        private ComputeBuffer golData;
        
        private static readonly int GoLData = Shader.PropertyToID("GoLData");
        private static readonly int Width = Shader.PropertyToID("Width");
        private static readonly int Height = Shader.PropertyToID("Height");
        private static readonly int BitWidth = Shader.PropertyToID("BitWidth");
        private static readonly int BitHeight = Shader.PropertyToID("BitHeight");


        private void OnEnable()
        {
            //Low VRAM video card friendly RT
            /*renderTexture = new RenderTexture((int)simulator.gridSize.x, (int)simulator.gridSize.y,0, RenderTextureFormat.R8)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Point
            };
            renderTexture.Create();
            meshRenderer.material.mainTexture = renderTexture;*/
            
            simulator.OnGridChanged += OnGridChanged;
        }

        private void OnGridChanged(uint2 size)
        {
           /* renderTexture.Release();
            renderTexture = new RenderTexture((int)size.x, (int)size.y,0, RenderTextureFormat.R8)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Point
            };
            renderTexture.Create();*/
            //meshRenderer.material.mainTexture = renderTexture;
            
            golData?.Dispose();
            var bufferSize = (int)(size.x*size.y/64);
            golData = new ComputeBuffer(bufferSize, sizeof(ulong), ComputeBufferType.Default,
                ComputeBufferMode.SubUpdates);
        }

        void Update()
        {
           
           /* if (simulator.currentImplementation == GoLSimulator.Implementation.FoneESquare
                || simulator.currentImplementation == GoLSimulator.Implementation.Liar
                || simulator.currentImplementation == GoLSimulator.Implementation.LiarWrap
                )
            {
                RenderSquareLayoutNoRT();
                return;
            }
            RenderHorizontalLayout();*/
           Rendering();
        }

        private void Rendering()
        {
            //var shader = squareRenderingShader;
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
            
            var buffer = golData.BeginWrite<uint2>(0, bufferSize);
            buffer.CopyFrom(simulator.Grid.Reinterpret<uint2>());
            
            golData.EndWrite<uint2>(bufferSize);
            meshRenderer.material.SetBuffer(GoLData, golData);
            
        }

        private void OnDisable()
        {
            
            golData?.Dispose();
            golData = null;
            
            simulator.OnGridChanged -= OnGridChanged;
        }


        [BurstCompile]
        public struct ArrayCopyJob<T> : IJobParallelForBatch where T : unmanaged
        {
            [ReadOnly] public NativeArray<T> source;
            [WriteOnly][NativeDisableUnsafePtrRestriction] public NativeArray<T> destination;
            
            [BurstCompile]
            public void Execute(int startIndex, int count)
            {
                //
                //source.GetSubArray(startIndex, count).CopyTo(destination.GetSubArray(startIndex, count));
                //var endIndex = startIndex + count;
                destination.GetSubArray(startIndex, count).CopyFrom(source.GetSubArray(startIndex, count)); 
                /*for(int i = startIndex; i < endIndex; i+=4)
                {
                    destination[i] = source[i];
                    destination[i+1] = source[i+1];
                    destination[i+2] = source[i+2];
                    destination[i+3] = source[i+3];
                }
                for(int i = endIndex - count % 4; i < endIndex; i++)
                {
                    destination[i] = source[i];
                }*/
            }
        }
    }
}