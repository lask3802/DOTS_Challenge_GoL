using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace LASK.GoL.TrivialJob
{
    public class GoLRenderer : MonoBehaviour
    {
        public GoLSimulator simulator;
        public ComputeShader shader;
        public Vector2 viewOffset;
        public float zoom;
        public RenderTexture renderTexture;
        public MeshRenderer meshRenderer;
        private ComputeBuffer golData;
        private static readonly int GoLData = Shader.PropertyToID("GoLData");
        private static readonly int Result = Shader.PropertyToID("Result");
        private static readonly int Width = Shader.PropertyToID("Width");

        private NativeArray<int> golBitsArray;

        private void OnEnable()
        {
            renderTexture = new RenderTexture(Screen.width, Screen.height,0, RenderTextureFormat.ARGB32)
            {
                enableRandomWrite = true
            };
            renderTexture.Create();
            meshRenderer.material.mainTexture = renderTexture;
            
        }

        unsafe void Update()
        {
            //load computeshader 
            
            var kernel = shader.FindKernel("CSMain");
            shader.SetTexture(kernel, Result, renderTexture);
            shader.SetInt(Width, renderTexture.width);
            var bufferSize = renderTexture.width*renderTexture.height/4;
            var gridSizeVector2Int = new Vector2Int((int)simulator.gridSize.x, (int)simulator.gridSize.y);
            if(golData == null)
                golData = new ComputeBuffer(bufferSize, sizeof(int), ComputeBufferType.Default, ComputeBufferMode.SubUpdates);
           
            var viewRect = new RectInt(Mathf.RoundToInt(viewOffset.x), Mathf.RoundToInt(viewOffset.y),
                renderTexture.width, renderTexture.height);
            
            var maxRowSize = (int)math.min(simulator.gridSize.x, viewRect.width) & 0x7FFFFFFC;
            var maxColSize = (int)math.min(simulator.gridSize.y, viewRect.height) & 0x7FFFFFFC;
         
            var buffer = golData.BeginWrite<int>(0, bufferSize);
            for (int y = 0; y < renderTexture.height; y++)
            {
                var bufferStartCoord = new Vector2Int((int)viewRect.x, (int)viewRect.y + y);
                var fromGridIdx = TopLeftBasedToIndex(new Vector2Int((int)viewRect.x, (int)viewRect.y + y), gridSizeVector2Int);
                fromGridIdx = (int)math.max(fromGridIdx, 0) & 0x7FFFFFFC;
                
                var arraySize = math.clamp(viewRect.xMin+maxRowSize, 0, simulator.gridSize.x);
                var ptr = simulator.Grid.GetSubArray((int)fromGridIdx, (int)arraySize).GetUnsafePtr();
                var dataArr =
                    CollectionHelper.ConvertExistingDataToNativeArray<int>(ptr, (int)arraySize / 4, Allocator.None);
                #if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref dataArr, NativeArrayUnsafeUtility.GetAtomicSafetyHandle(simulator.Grid));
                #endif
                var computeBufferIdx = (int)((y+viewOffset.y) * renderTexture.width+viewOffset.x) / 4;
                buffer.Slice(computeBufferIdx, dataArr.Length).CopyFrom(dataArr);
               
            }
            golData.EndWrite<int>(bufferSize);
            shader.SetBuffer(0, GoLData, golData);
            shader.Dispatch(kernel, (int)maxRowSize / 4, renderTexture.height, 1);
            
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
        }
    }
}