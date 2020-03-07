using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GravityTest : Movement
{

    private void Start()
    {
        //StartCoroutine("ConrotineTest");

        //yield return new WaitForSeconds(1);
        //StopCoroutine("ConrotineTest");
       Rigidbody aaa = transform.GetComponent<Rigidbody>();
    }
    void Update()
    {
        
    }
    /// <summary>
    /// 抵消重力
    /// </summary>
    private void FixedUpdate()
    {
        //rb.AddForce(-Physics.gravity * rb.mass); ;
    }

    public IEnumerator ConrotineTest()
    {
        while (true)
        {
            Debug.LogError("Running");
            yield return 1;
        }
    }
}
