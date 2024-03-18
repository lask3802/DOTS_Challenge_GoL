using System;
using System.Collections;
using System.Collections.Generic;
using DynamicSize.Component;
using TMPro;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace DynamicSize.UI
{
    public class DynamicGoLUI : MonoBehaviour
    {
        
        public TMP_InputField gridSizeInput;
        public TextMeshProUGUI activeCountText;
        
        
        public int GridSize { get; private set; } = 64;
        public bool IsReSimulate { get; set; }
        public bool IsInitialized { get; private set; }
        private Entity entity;
        void Start()
        {
            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var e = entityManager.CreateEntity(ComponentType.ReadWrite<DynamicGoLUI>());
            entityManager.AddComponentObject(e,this);
            
        }
        
        public void OnEditGridSize(string newSize)
        {
            var size = ParseSize(newSize, out var fixedSize);
            if(fixedSize != size)
                gridSizeInput.text = $"{fixedSize}";
        }

        private int ParseSize(string newSize, out int fixedSize)
        {
            var size = int.Parse(newSize);
            fixedSize = Math.Clamp(size, 1, SystemInfo.maxTextureSize);
            GridSize = fixedSize;
            return size;
        }

        public void ActiveCountChange(float count)
        {
            var cntInt = Mathf.RoundToInt(count);
            activeCountText.text = $"X{cntInt}";
        }
        
        public void InitGridSize(int size)
        {
            if (IsInitialized) return;
            gridSizeInput.text = $"{size}";
            GridSize = size;
            IsInitialized = true;

        }
        
        public void ReSimulation()
        {
            var size = uint.Parse(gridSizeInput.text);
            IsReSimulate = true;
        }

      
    }
}
