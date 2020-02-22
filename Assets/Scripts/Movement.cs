using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Movement : MonoBehaviour
{
    [HideInInspector]
    public Rigidbody2D rb;
    
    protected Collider2D coll;


    //protected Vector2 currentMove;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        coll = GetComponent<Collider2D>();
        //anim = GetComponent<Animator>();
    }


    private void FixedUpdate()
    {
        //rb.velocity = currentMove * Time.fixedDeltaTime;
    }


}
