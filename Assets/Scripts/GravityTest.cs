using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GravityTest : Movement
{
    void Update()
    {
        
    }
    /// <summary>
    /// 抵消重力
    /// </summary>
    private void FixedUpdate()
    {
        rb.AddForce(-Physics.gravity * rb.mass); ;
    }
}
