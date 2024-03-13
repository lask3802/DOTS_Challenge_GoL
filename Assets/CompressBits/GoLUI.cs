using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

namespace LASK.GoL.CompressBits
{
    public class GoLUI : MonoBehaviour
    {
        public GoLSimulator simulator;
        public TMP_InputField gridSizeInput;
        
        void Start()
        {
            gridSizeInput.text = $"{simulator.gridSize.x}";
        }
        
        public void OnEditGridSize(string newSize)
        {
            var size = uint.Parse(newSize);
            var fixedSize = Math.Clamp(size/64*64, 64, (uint)SystemInfo.maxTextureSize);
            if(fixedSize != size)
                gridSizeInput.text = $"{fixedSize}";
            
        }
        
        public void OnApplyGridSize()
        {
            var size = uint.Parse(gridSizeInput.text);
            simulator.isPaused = true;
            simulator.ResetGrid(size);
        }

        public void OnFirstAttemptCheck()
        {
            simulator.currentImplementation = GoLSimulator.Implementation.FirstAttempt;
        }
        
        public void OnSecondAttemptCheck()
        {
            simulator.currentImplementation = GoLSimulator.Implementation.SecondAttempt;
        }

        public void OnFoneECheck()
        {
            simulator.currentImplementation = GoLSimulator.Implementation.FoneE;
        }
    }
}