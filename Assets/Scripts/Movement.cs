using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Movement : MonoBehaviour
{
    [HideInInspector]
    public Rigidbody2D rb;
    
    protected Collider2D coll;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        coll = GetComponent<Collider2D>();
        //anim = GetComponent<Animator>();

        Initiate();
    }

    /// <summary>
    /// 初始化方法，实现在派生类
    /// </summary>
    public virtual void Initiate() { }

}
