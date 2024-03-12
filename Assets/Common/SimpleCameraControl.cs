using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class SimpleCameraControl : MonoBehaviour
{
    Camera cam;
    public float zoomSpeed = 1;
    public float dragSpeed = 1;
    private Vector2 dragStartPosition;
    void Awake()
    {
        cam = GetComponent<Camera>();
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
       if(Mouse.current.leftButton.wasPressedThisFrame)
       {
          dragStartPosition = Mouse.current.position.ReadValue();
          return;
       }
       
       if(Mouse.current.leftButton.isPressed)
       {
           var dragDelta = dragStartPosition - Mouse.current.position.ReadValue();
           cam.transform.Translate(dragDelta * cam.orthographicSize / 1000*dragSpeed);
           dragStartPosition = Mouse.current.position.ReadValue();
       }
       
       if(Mouse.current.scroll.ReadValue().y != 0)
       {
           cam.orthographicSize = Math.Max(cam.orthographicSize - Mouse.current.scroll.ReadValue().y*zoomSpeed, 0.1f);
       }
    }
}
