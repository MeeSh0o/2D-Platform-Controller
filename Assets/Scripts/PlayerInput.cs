using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInput : Movement
{ 
    [Header("Basic Moving")]
    public float speed;
    public float jumpForce;

    // 仅用于监视，表示滞空类型
    [Header("State")]
    public StateEnum state;

    [Header("Status")]
    public bool isGround;
    public bool isJump;

    // 不同状态下的引力倍数
    [Header("Gravity Multiplier")]
    public float upGravityMultiplier = 1f;
    public float releaseGravityMultiplier = 2f;
    public float downGravityMultiplier = 2.5f;

    [Space]
    public Transform groundCheck;
    public LayerMask ground;

    [Space]
    public int jumpCount;

    bool jumpPressed;
    bool isFlixed = false;
    PhysicsMaterial2D pM2D;



    private void Awake()
    {
        pM2D = GetComponent<Rigidbody2D>().sharedMaterial;
    }

    void Update()
    {
        if (Input.GetButtonDown("Jump") && jumpCount > 0)
        {
            jumpPressed = true;
        }
    }

    private void FixedUpdate()
    {
        StateCheck();
        GroundMovement();
        Jump();
        BetterGravity();
    }

    void GroundMovement()
    {
        float horizontalMove = Input.GetAxis("Horizontal");
        rb.velocity = new Vector2(horizontalMove * speed, rb.velocity.y);

        if (isFlixed ? (rb.velocity.x > 0.01f) : (rb.velocity.x < 0.01f))
        {
            isFlixed = !isFlixed;
            transform.localScale = new Vector3(-transform.localScale.x, transform.localScale.y, transform.localScale.z);
        }

    }

    bool Jump()
    {
        if (isGround)
        {
            jumpCount = 2;
            isJump = false;
        }
        if (jumpPressed && isGround)
        {
            isJump = true;
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            jumpCount--;
            jumpPressed = false;
            return true;
        }
        else if (jumpPressed && jumpCount > 0 && !isGround)
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            jumpCount--;
            jumpPressed = false;
            return false;
        }
        return false;
    }

    void BetterGravity()
    {
        if (rb.velocity.y < 0)
        {
            state = StateEnum.Drop;
            rb.velocity += Vector2.up * Physics2D.gravity.y * (downGravityMultiplier - 1) * Time.fixedDeltaTime;
        }
        else if (rb.velocity.y > 0 && !Input.GetButton("Jump"))
        {
            state = StateEnum.ReleaseJump;
            rb.velocity += Vector2.up * Physics2D.gravity.y * (releaseGravityMultiplier - 1) * Time.fixedDeltaTime;
        }
        else if (rb.velocity.y > 0 && Input.GetButton("Jump"))
        {
            state = StateEnum.Jumping;
            rb.velocity += Vector2.up * Physics2D.gravity.y * (upGravityMultiplier - 1) * Time.fixedDeltaTime;
        }
        else state = StateEnum.Default;
    }

    void StateCheck()
    {
        isGround = Physics2D.OverlapCircle(groundCheck.position, 0.1f, ground);
    }

    void StateChange(StateEnum para)
    {
        state = para;
    }
}

public enum StateEnum
{
    Default = 0,
    Jumping = 1,
    Drop = 2,
    ReleaseJump = 3
}