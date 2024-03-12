using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace LASK.GoL.CompressBits
{
   public class GoLRenderer : MonoBehaviour
    {
        public GoLSimulator simulator;
        public ComputeShader shader;
        public RenderTexture renderTexture;
        public MeshRenderer meshRenderer;
        private ComputeBuffer golData;
        private static readonly int GoLData = Shader.PropertyToID("GoLData");
        private static readonly int Result = Shader.PropertyToID("Result");
        private static readonly int Width = Shader.PropertyToID("Width");
    

        private void OnEnable()
        {
            renderTexture = new RenderTexture((int)simulator.gridSize.x, (int)simulator.gridSize.y,0, RenderTextureFormat.ARGB32)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Point
            };
            renderTexture.Create();
            meshRenderer.material.mainTexture = renderTexture;
            
            simulator.OnGridChanged += OnGridChanged;
        }

        private void OnGridChanged(uint2 size)
        {
            renderTexture.Release();
            renderTexture = new RenderTexture((int)size.x, (int)size.y,0, RenderTextureFormat.ARGB32)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Point
            };
            renderTexture.Create();
            meshRenderer.material.mainTexture = renderTexture;
        }

        void Update()
        {
            //load computeshader 
            
            var kernel = shader.FindKernel("CSMain");
            shader.SetTexture(kernel, Result, renderTexture);
            
            var bufferSize = renderTexture.width*renderTexture.height/64;
            if (golData == null || bufferSize != golData.count)
            {
                golData?.Release();
                golData = new ComputeBuffer(bufferSize, sizeof(ulong), ComputeBufferType.Default,
                    ComputeBufferMode.SubUpdates);
            }

            var maxRowSize = (int)simulator.gridSize.x / 64;
            shader.SetInt(Width, (int)simulator.gridSize.x/64);
            
            var buffer = golData.BeginWrite<uint2>(0, bufferSize);
            buffer.CopyFrom(simulator.Grid.Reinterpret<uint2>());
            golData.EndWrite<uint2>(bufferSize);
            
            shader.SetBuffer(0, GoLData, golData);
            shader.Dispatch(kernel, maxRowSize, renderTexture.height, 1);
            
        }
        
       
        
        private static int TopLeftBasedToIndex(Vector2Int topLeftBased, Vector2Int gridSize){
            return (int)topLeftBased.y * (int)gridSize.x + (int)topLeftBased.x;
        }

        private static int CenterBasedToIndex(Vector2Int centerBased, Vector2Int gridSize)
        {
            return TopLeftBasedToIndex(centerBased + gridSize / 2+new Vector2Int(centerBased.x/2,0), gridSize);
        }
        
        

        private void OnDisable()
        {
            renderTexture.Release();
            renderTexture = null;
            
            golData.Release();
            golData = null;
            
            simulator.OnGridChanged -= OnGridChanged;
        }
    }
}