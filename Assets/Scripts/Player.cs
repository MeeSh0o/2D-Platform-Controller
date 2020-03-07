using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class Player : Movement
{
    [Header("Ability")] // 能力
    public bool jump = true;
    public bool wallGrab = true;
    public bool wallSlice = true;
    public bool wallClimb = true;
    public bool wallJump = true;
    public bool horizontalDash = true;
    public bool verticalDash;
    public bool octDash;

    [Header("Ability Parameter")]
    public float moveAccelerationTime = 0.3f; // 移动到满速的时间
    public float moveDragTime = 0.2f; // 地面减速时间
    public float speed = 8;
    public float jumpForce = 17.5f;
    public float airMoveAccelerationTime = 0.5f; // 空中移动到满速的时间
    public float airDrag = 0.3f; // 空中减速时间
    public int airJumpCount = 1; // 允许跳跃次数
    public float dashDistance = 7; // 冲刺距离
    public float dashTimeLast = .8f; // 冲刺持续时间
    public int dashCount = 1; // 允许冲刺次数
    public float wolfJumpTime = 0.05f; // 狼跳时间

    [Header("State")] // 状态
    public StateEnum state;

    [Header("Status")] // 属性
    public bool isGround;
    public bool isJump;
    public bool byWall;
    public int wallSide; // 1为左，-1为右,0为不靠墙或者两边都靠墙，可根据bywall区分
    public bool isGrab;
    public bool isSlice;
    public bool canMove = true;
    public bool canJump = true;
    public bool canDash = true;

    [Header("Wall About")]
    public float climbSpeed; // 上爬速度
    public float sliceSpeed; // 下滑速度，无体力下滑同速
    public bool useStamina; // 是否启用耐力
    public float stamina; // 耐力
    public float grabCost; // 抓墙消耗耐力per sec
    public float jumpCost; // 抓墙的墙跳，一次扣除
    public float climbCost; // 爬墙消耗耐力per sec
    public float sliceCost; // 滑墙消耗耐力per sec ，一般设置为0
    public float wallJumpHroi; // 靠墙跳横向拆分
    public float noStaminaWallJumpHori; // 无体力时墙跳横向拆分
    public float ezWallJumpTime; // 允许多久提前墙跳
    public float stickForce; // 向墙黏附的力度

    [Header("Freeze Time")] // 禁止行动时间
    public float jumpFreezeTime; // 平地跳
    public float airJumpFreezeTime; // 空中跳
    public float onWallJumpFreezeTime; // 上墙墙跳
    public float byWallJumpFreezeTime; // 靠墙墙跳
    public float dashFreezeTime; // 冲刺

    [Header("Gravity Multiplier")]// 不同状态下的引力倍数
    public float jumpGravityMultiplier = 1f; // 跳跃中
    public float releaseGravityMultiplier = 3.333333f; // 释放跳跃键的跳跃中
    public float dropGravityMultiplier = 2.5f; // 下落

    [Header("Ray Cast")]
    public LayerMask checkLayer;
    public Vector3[] leftRayPositin;
    public Vector3[] rightRayPosition;
    public Vector3[] bottomRayPosition;
    public float rayLength;

    [Space]// 过程中参数
    public int currentJumpCount = 0; // 当前跳跃剩余次数
    public float currentDashCount = 0; // 当前冲刺剩余次数
    public float currentStamina = 0;
    bool jumpPressed;
    bool dashPressed;
    int facingRight = 1; // 面朝右时为正1，左为负1
    public static float defaultGravityScale = 3; // 默认重力倍数
    private float moveAccelerate; // 移动加速度
    private float airAccelerate; // 空中加速度
    private float dashSpeed; // 冲刺的速度
    public Movement underMovementObject = null; // 脚下的物体
    public Movement leftMovementObject = null; // 左边的的物体
    public Movement rightMovementObject = null; // 右边的物体
    public Movement currentMovementObject = null; // 当前附着的物体
    private float currentWolfJumpTime; // 狼跳计时
    float ezJumpTimer; // 使得提前按跳也能生效
    Vector2 colliderSize; // 用于投射的碰撞体大小,如果换了碰撞体，需把jump()里的投射自己处理一下
    Coroutine currentFreezeMove = null;
    Coroutine currentFreezeJump = null;
    Coroutine currentDash = null;
    delegate void FreeAction();

    /// <summary>
    /// 角色状态枚举
    /// </summary>
    public enum StateEnum
    {
        Default = 0, // 默认状态，除了初始化不应该出现
        Ground, // 地面，非，则为在空中
        Jump, // 跳跃中，按住jump button过程中
        Drop, // 坠落
        ReleaseJump, // 释放jump button后的上升期间，仅用于改变重力
        WallJump, // 墙跳
        WallGrab, // climb是grab下一状态
        WallSlice, // 当体力耗尽的抓墙
        Dash // 冲刺
    }

    void Update()
    {
        EzJump();
        InputManager();
        WolfJumpTimer();
        DrawDebugLine();
        FacingDir();
    }

    private void FixedUpdate()
    {
        EzJump();
        RayCheck(); // 先得出碰撞数据
        StateCheck(); // 再得出状态数据，有些状态不由行动而来，放在这里
        // 之后根据行动进一步改变状态
        Dash();
        WallAction();
        Jump();
        PositiveMovement();
        //BetterGravity(); //必须放在StateCheck之后 // 现在是在任意状态改变的时候，无所谓位置了
        //AttachedMovement(); // 有bug TODO 修
    }

    /// <summary>
    /// 初始化方法
    /// </summary>
    public override void Initiate()
    {
        if (rb.gravityScale != defaultGravityScale) rb.gravityScale = defaultGravityScale;
        colliderSize = GetComponent<BoxCollider2D>().size;
        if (useStamina) StaminaReset();
        currentDashCount = dashCount;
    }

    void InputManager()
    {
        if (Input.GetButtonDown("Jump"))
        {
            jumpPressed = true;
        }
        if (Input.GetButtonDown("Fire1"))
        {
            dashPressed = true;
        }
    }

    void PositiveMovement()
    {
        if (!canMove)
            return;

        if (state.Equals(StateEnum.WallGrab) || state.Equals(StateEnum.WallSlice))
            return;

        float horizontalMove = Input.GetAxisRaw("Horizontal") * speed;


        if (Mathf.Abs(rb.velocity.x) > Mathf.Abs(horizontalMove)) // 超速状态下 减速
        {
            if (horizontalMove * rb.velocity.x <= 0 || state.Equals(StateEnum.Ground))// 只有按住按键才能保持超速，否则减速,落地减速
            {
                float drag = 0;
                switch (state)
                {
                    case StateEnum.Drop:
                    case StateEnum.Jump:
                    case StateEnum.ReleaseJump:
                    case StateEnum.WallJump:
                        drag = speed / airDrag;
                        break;
                    case StateEnum.Default:
                    case StateEnum.Ground:
                        drag = speed / moveDragTime;
                        break;
                }
                rb.velocity = new Vector2(Mathf.Lerp(rb.velocity.x, horizontalMove, drag * Time.fixedDeltaTime), rb.velocity.y);
            }

        }
        else // 加速
        {
            float accelerate = 0;
            switch (state)
            {
                case StateEnum.Drop:
                case StateEnum.Jump:
                case StateEnum.ReleaseJump:
                case StateEnum.WallJump:
                    accelerate = speed / airMoveAccelerationTime;
                    break;
                case StateEnum.Default:
                case StateEnum.Ground:
                    accelerate = speed / moveAccelerationTime;
                    break;
            }
            rb.velocity = new Vector2(Mathf.Lerp(rb.velocity.x, horizontalMove, accelerate * Time.fixedDeltaTime), rb.velocity.y);
        }
    }

    bool Jump()
    {
        if (!jump) return false;

        if (!canJump) return false;

        if (!jumpPressed) return false;

        if ((state.Equals(StateEnum.Ground) || currentWolfJumpTime > 0)) // 平地跳
        {
            isJump = true;
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            jumpPressed = false;

            StateChange(StateEnum.Jump);

            FreezeJump(jumpFreezeTime);

            Debug.Log("jump");
            return true;
        }
        else if (byWall) // 墙跳
        {
            isJump = true;
            if (wallSide == 0)
            {
                rb.velocity = new Vector2(0, jumpForce);
            }
            else rb.velocity = new Vector2(wallJumpHroi * wallSide, jumpForce).normalized * jumpForce;
            jumpPressed = false;

            StateChange(StateEnum.WallJump);

            if (state.Equals(StateEnum.WallGrab)) // 使得按住墙方向时跳仍然可以跳出去
            {
                FreezeMove(onWallJumpFreezeTime);
                FreezeJump(onWallJumpFreezeTime);
            }
            else
            {
                FreezeMove(byWallJumpFreezeTime);
                FreezeJump(jumpFreezeTime);
            }

            Debug.Log("wall jump");
            return true;
        }
        else if (currentJumpCount > 0) // 空中跳
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            currentJumpCount--;
            jumpPressed = false;
            StateChange(StateEnum.Jump);
            FreezeJump(airJumpFreezeTime);
            Debug.Log("air jump");
            return true;
        }
        else if
        (
            Physics2D.Raycast(transform.position + bottomRayPosition[0], Vector2.down, rayLength * ezJumpTimer, checkLayer)
            || Physics2D.Raycast(transform.position + bottomRayPosition[1], Vector2.down, rayLength * ezJumpTimer, checkLayer)
            || Physics2D.Raycast(transform.position + bottomRayPosition[2], Vector2.down, rayLength * ezJumpTimer, checkLayer)
        ) // 没跳,检测使得稍微落地前一点按跳也能算跳
        {
            return false;
        }
        jumpPressed = false;
        return false;
    }

    /// <summary>
    /// 墙面作用
    /// </summary>
    void WallAction()
    {
        if (!canMove) return;
        if (byWall)
        {
            Vector2 wallVelocity = Vector2.zero;

            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");

            if (horizontal * wallSide > 0) // 输入墙的反向，则离开墙状态
            {
                if (state.Equals(StateEnum.WallGrab) || state.Equals(StateEnum.WallSlice))
                {
                    StateChange(StateEnum.Default);
                }
                return;
            }
            // 设定为不输入按键可以停留在墙上
            if (horizontal * wallSide < 0 || (wallSide == 0 && horizontal != 0)) // 输入墙的方向,进入攀附或者滑墙
            {
                if (wallGrab)
                {
                    StateChange(StateEnum.WallGrab);
                }
                else if (wallSlice)
                {
                    StateChange(StateEnum.WallSlice);
                }
            }

            if (state.Equals(StateEnum.WallGrab)) // 攀附
            {
                if((Mathf.Abs(vertical) < 0.01 || !wallClimb)) // 无垂直输入或者禁止爬墙
                {
                    if (useStamina ? currentStamina > 0 : true) // 有体力
                    {
                        StaminaComputer(grabCost);
                    }
                    else if(wallSlice) // 无体力
                    {
                        StateChange(StateEnum.WallSlice);
                    }
                    else StateChange(StateEnum.Default);
                }
                else if(vertical >= 0.01 && wallClimb) // 垂直输入为上且可爬墙
                {
                    if (useStamina ? currentStamina > 0 : true) // 有体力
                    {
                        wallVelocity += Vector2.up * climbSpeed;
                        StaminaComputer(climbCost);
                    }
                    else if (wallSlice) // 无体力
                    {
                        StateChange(StateEnum.WallSlice);
                    }
                    else StateChange(StateEnum.Default);
                }
                else if(vertical <= .01 && wallClimb) // 垂直输入为下且可爬墙
                {
                    if (useStamina ? currentStamina > 0 : true) // 有体力
                    {
                        wallVelocity += Vector2.down * sliceSpeed;
                        StaminaComputer(sliceCost);
                    }
                    else if (wallSlice) // 无体力
                    {
                        StateChange(StateEnum.WallSlice);
                    }
                    else StateChange(StateEnum.Default);
                }
            }
            if(state.Equals(StateEnum.WallSlice)) // 滑墙
            {
                wallVelocity += Vector2.down * sliceSpeed;
            }

            // 往墙吸附，因为这一帧已经不能移动了，所以需要额外提供速度。
            if(state.Equals(StateEnum.WallGrab) || state.Equals(StateEnum.WallSlice))
                wallVelocity += StickToWall(horizontal);

            if (state.Equals(StateEnum.WallGrab) || state.Equals(StateEnum.WallSlice))
            {
                rb.velocity = wallVelocity;
            }
        }
        else if (state.Equals(StateEnum.WallGrab) || state.Equals(StateEnum.WallSlice))
        {
            StateChange(StateEnum.Default);
        }
    }

    /// <summary>
    /// 向墙吸附
    /// </summary>
    /// <param name="hori"></param>
    Vector2 StickToWall(float hori)
    {
        return new Vector2(hori * stickForce, 0);
    }

    /// <summary>
    /// 耐力计算器
    /// </summary>
    void StaminaComputer(float para = -1)
    {
        if (useStamina)
        {
            if (para > -0.01)
            {
                currentStamina -= para * Time.fixedDeltaTime;
                if (currentStamina < 0) currentStamina = 0;
            }
        }
    }

    /// <summary>
    /// 耐力重置
    /// </summary>
    public void StaminaReset()
    {
        currentStamina = stamina;
    }

    /// <summary>
    /// 冲刺
    /// </summary>
    bool Dash()
    {
        if (!verticalDash) return dashPressed = false;

        // 判断退出Dash
        // Pass  先做只有计时退出的

        if (!canDash) return dashPressed = false;
        
        if (currentDashCount <= 0) return dashPressed = false;

        if(Input.GetAxisRaw("Horizontal") == 0) return dashPressed = false; // 暂时不处理面向问题

        if (dashPressed) 
        {
            DashIn();
            FreezeMove(dashFreezeTime);
        }

        return dashPressed = false;
    }
    void DashIn()
    {
        StateChange(StateEnum.Dash);
        if (currentDash != null)
            StopCoroutine(currentDash);
        currentDash = StartCoroutine(Timer(DashOut, dashTimeLast));

        float speed = dashDistance / dashTimeLast;
        
        if (octDash)
        {
            rb.velocity = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized * speed;
        }
        else
        {
            rb.velocity = new Vector2(Input.GetAxisRaw("Horizontal") * speed, 0);
        }

        
    }
    void DashOut()
    {
        if(currentFreezeMove != null)
        {
            StopCoroutine(currentFreezeMove);
            FreeMove();
        }

        StateChange(StateEnum.Default);
        currentDashCount -= 1;
    }

    /// <summary>
    /// 更好的重力曲线
    /// </summary>
    void BetterGravity()
    {
        if (state.Equals(StateEnum.Drop))
        {
            //rb.velocity += Vector2.up * Physics2D.gravity.y * (dropGravityMultiplier - 1) * Time.fixedDeltaTime;
            //rb.AddForce(Physics.gravity * rb.mass * (dropGravityMultiplier - 1));
            rb.gravityScale = defaultGravityScale * dropGravityMultiplier;
        }
        else if (state.Equals(StateEnum.ReleaseJump))
        {
            //rb.velocity += Vector2.up * Physics2D.gravity.y * (releaseGravityMultiplier - 1) * Time.fixedDeltaTime;
            //rb.AddForce(Physics.gravity * rb.mass * (releaseGravityMultiplier - 1));
            rb.gravityScale = defaultGravityScale * releaseGravityMultiplier;
        }
        else if (state.Equals(StateEnum.WallGrab) || state.Equals(StateEnum.WallSlice) || state.Equals(StateEnum.Dash))
        {
            rb.gravityScale = 0;
        }
        else if (state.Equals(StateEnum.Jump) || state.Equals(StateEnum.WallJump))
        {
            //rb.velocity += Vector2.up * Physics2D.gravity.y * (jumpGravityMultiplier - 1) * Time.fixedDeltaTime;
            //rb.AddForce(Physics.gravity * rb.mass * (jumpGravityMultiplier - 1));
            rb.gravityScale = defaultGravityScale * jumpGravityMultiplier;
        }
        else rb.gravityScale = defaultGravityScale;

    }

    /// <summary>
    /// 检查状态
    /// </summary>
    void StateCheck()
    {
        //isGround = Physics2D.OverlapCircle(groundCheck.position, 0.1f, ground);


        if (isGround)
        {
            StateChange(StateEnum.Ground);
        }
        else if (rb.velocity.y < 0 && !isGround)
        {
            StateChange(StateEnum.Drop);
        }
        else if (!Input.GetButton("Jump") && state.Equals(StateEnum.Jump) && jump)
        {
            StateChange(StateEnum.ReleaseJump);
        }
        //else if (rb.velocity.y > 0 && Input.GetButton("Jump") && jump)
        //{
        //    StateChange(StateEnum.Jump);
        //}
    }

    /// <summary>
    /// 状态转换
    /// </summary>
    /// <param name="para"></param>
    void StateChange(StateEnum para)
    {
        state = para;
        BetterGravity();
        switch (para)
        {
            case StateEnum.Ground:
                StaminaReset();
                currentJumpCount = airJumpCount;
                isJump = false;
                currentDashCount = dashCount;
                break;
        }

    }

    /// <summary>
    /// 狼跳计时
    /// </summary>
    private void WolfJumpTimer()
    {
        if (state.Equals(StateEnum.Ground))
        {
            currentWolfJumpTime = wolfJumpTime;
        }
        else if (currentWolfJumpTime > 0)
        {
            currentWolfJumpTime -= Time.deltaTime;
        }
    }

    RaycastHit2D leftH, leftM, leftL, rightH, rightM, rightL, bottomM, bottomL, bottomR;

    //检测，使用三根射线
    void RayCheck()
    {
        leftH = Physics2D.Raycast(transform.position + leftRayPositin[0], Vector2.left, rayLength, checkLayer);
        leftM = Physics2D.Raycast(transform.position + leftRayPositin[1], Vector2.left, rayLength, checkLayer);
        leftL = Physics2D.Raycast(transform.position + leftRayPositin[2], Vector2.left, rayLength, checkLayer);
        rightH = Physics2D.Raycast(transform.position + rightRayPosition[0], Vector2.right, rayLength, checkLayer);
        rightM = Physics2D.Raycast(transform.position + rightRayPosition[1], Vector2.right, rayLength, checkLayer);
        rightL = Physics2D.Raycast(transform.position + rightRayPosition[2], Vector2.right, rayLength, checkLayer);
        bottomL = Physics2D.Raycast(transform.position + bottomRayPosition[0], Vector2.down, rayLength, checkLayer);
        bottomM = Physics2D.Raycast(transform.position + bottomRayPosition[1], Vector2.down, rayLength, checkLayer);
        bottomR = Physics2D.Raycast(transform.position + bottomRayPosition[2], Vector2.down, rayLength, checkLayer);

        if (bottomM || bottomL || bottomR) //着地
        {
            isGround = true;
            if (bottomM)
            {
                if (bottomM.transform.GetComponent<Movement>())
                    underMovementObject = bottomM.transform.GetComponent<Movement>();
            }
            else if (!bottomL && bottomR.transform.GetComponent<Movement>())
            {
                underMovementObject = bottomR.transform.GetComponent<Movement>();
            }
            else if (!bottomR && bottomL.transform.GetComponent<Movement>())
            {
                underMovementObject = bottomL.transform.GetComponent<Movement>();
            }
            else underMovementObject = null;
        }
        else
        {
            isGround = false;
        }

        if (leftM || leftL || leftH || rightM || rightL || rightH)
        {
            byWall = true;
            wallSide = WallSideJudge(leftM, leftL, leftH, rightM, rightL, rightH);
            //leftMovementObject = CheckMovementObject(leftM, leftL, leftH);
            //rightMovementObject = CheckMovementObject(rightM, rightL, rightH);
        }
        else
        {
            byWall = false;
            wallSide = 0;
        }
    }

    /// <summary>
    /// 判断墙的方向
    /// </summary>
    int WallSideJudge(bool l1, bool l2, bool l3, bool r1, bool r2, bool r3)
    {
        if ((l1 || l2 || l3) && (!r1 && !r2 && !r3))
        {
            return 1;
        }
        else if ((r1 || r2 || r3) && (!l1 && !l2 && !l3))
        {
            return -1;
        }
        return 0;
    }

    /// <summary>
    /// 存在找不到对象的bug TODO修
    /// </summary>
    /// <param name="m"></param>
    /// <param name="l"></param>
    /// <param name="h"></param>
    /// <returns></returns>
    Movement CheckMovementObject(RaycastHit2D m, RaycastHit2D l, RaycastHit2D h)
    {
        if (m || l || h) //靠墙
        {
            if (m)
            {
                if (m.transform.GetComponent<Movement>())
                {
                    return m.transform.GetComponent<Movement>();
                }
            }
            else if (l.transform.GetComponent<Movement>()) // 中间没有墙而上下有墙时，优先下。这里应该使环境速度 = 两墙速度之均值，形成拉扯感，但是嫌麻烦，如果之后没有需求，就不做。
            {
                return l.transform.GetComponent<Movement>();
            }
            else if (h.transform.GetComponent<Movement>())
            {
                return h.transform.GetComponent<Movement>();
            }
        }
        return null;
    }

    /// <summary>
    /// 轻松地面跳跃
    /// </summary>
    private void EzJump()
    {
        float velocity = rb.velocity.magnitude;
        if (velocity > speed * 3) ezJumpTimer = 15;
        else if (velocity > speed * 2) ezJumpTimer = 10;
        else if (velocity > speed) ezJumpTimer = 5;
        else ezJumpTimer = 2;
    }

    /// <summary>
    /// 绘制检测线
    /// </summary>
    void DrawDebugLine()
    {
        Debug.DrawLine(transform.position + leftRayPositin[0], transform.position + leftRayPositin[0] + Vector3.left * rayLength * ezJumpTimer, Color.red);
        Debug.DrawLine(transform.position + leftRayPositin[1], transform.position + leftRayPositin[1] + Vector3.left * rayLength * ezJumpTimer, Color.red);
        Debug.DrawLine(transform.position + leftRayPositin[2], transform.position + leftRayPositin[2] + Vector3.left * rayLength * ezJumpTimer, Color.red);
        Debug.DrawLine(transform.position + rightRayPosition[0], transform.position + rightRayPosition[0] + Vector3.right * rayLength * ezJumpTimer, Color.red);
        Debug.DrawLine(transform.position + rightRayPosition[1], transform.position + rightRayPosition[1] + Vector3.right * rayLength * ezJumpTimer, Color.red);
        Debug.DrawLine(transform.position + rightRayPosition[2], transform.position + rightRayPosition[2] + Vector3.right * rayLength * ezJumpTimer, Color.red);

        Debug.DrawLine(transform.position + bottomRayPosition[0], transform.position + bottomRayPosition[0] + Vector3.down * rayLength * ezJumpTimer, Color.red);
        Debug.DrawLine(transform.position + bottomRayPosition[1], transform.position + bottomRayPosition[1] + Vector3.down * rayLength * ezJumpTimer, Color.red);
        Debug.DrawLine(transform.position + bottomRayPosition[2], transform.position + bottomRayPosition[2] + Vector3.down * rayLength * ezJumpTimer, Color.red);

        Debug.DrawLine(transform.position + leftRayPositin[0], transform.position + leftRayPositin[0] + Vector3.left * rayLength, Color.yellow);
        Debug.DrawLine(transform.position + leftRayPositin[1], transform.position + leftRayPositin[1] + Vector3.left * rayLength, Color.yellow);
        Debug.DrawLine(transform.position + leftRayPositin[2], transform.position + leftRayPositin[2] + Vector3.left * rayLength, Color.yellow);
        Debug.DrawLine(transform.position + rightRayPosition[0], transform.position + rightRayPosition[0] + Vector3.right * rayLength, Color.yellow);
        Debug.DrawLine(transform.position + rightRayPosition[1], transform.position + rightRayPosition[1] + Vector3.right * rayLength, Color.yellow);
        Debug.DrawLine(transform.position + rightRayPosition[2], transform.position + rightRayPosition[2] + Vector3.right * rayLength, Color.yellow);

        Debug.DrawLine(transform.position + bottomRayPosition[0], transform.position + bottomRayPosition[0] + Vector3.down * rayLength, Color.blue);
        Debug.DrawLine(transform.position + bottomRayPosition[1], transform.position + bottomRayPosition[1] + Vector3.down * rayLength, Color.blue);
        Debug.DrawLine(transform.position + bottomRayPosition[2], transform.position + bottomRayPosition[2] + Vector3.down * rayLength, Color.blue);
    }

    /// <summary>
    /// 管理面朝方向
    /// </summary>
    void FacingDir(int right = 0)
    {
        // 逻辑换向 没有攀附看速度，脚底有攀附看输入，攀墙看墙
        if (right == 0)
        {
            if (state.Equals(StateEnum.WallGrab) || state.Equals(StateEnum.WallSlice)) // 除了攀附以外的情况都可以自动化，攀附则根据攀附对象决定朝向
            {
                facingRight = rb.velocity.x > 0 ? 1 : -1;
            }
        }
        else facingRight = right;

        // 表现换向
        //if (isFlixed ? (rb.velocity.x > 0.01f) : (rb.velocity.x < 0.01f))
        //{
        //    isFlixed = !isFlixed;
        //    transform.localScale = new Vector3(-transform.localScale.x, transform.localScale.y, transform.localScale.z);
        //}
    }

    /// <summary>
    /// 倒计时工具
    /// </summary>
    private IEnumerator Timer(FreeAction freeAction, float time)
    {
        yield return 0;
        yield return new WaitForSeconds(time);
        freeAction();
        yield return null;
    }



    /// <summary>
    /// 禁止跳跃，我也不知道有啥用……反正先写了
    /// </summary>
    /// <param name="time"></param>
    void FreezeJump(float time)
    {
        canJump = false;
        if (currentFreezeJump != null)
            StopCoroutine(currentFreezeJump);
        currentFreezeJump = StartCoroutine(Timer(FreeJump, time));
    }
    public void FreeJump()
    {
        if (currentFreezeJump != null)
            StopCoroutine(currentFreezeJump);
        canJump = true;
    }

    /// <summary>
    /// 禁止移动
    /// </summary>
    /// <param name="time"></param>
    void FreezeMove(float time)
    {
        canMove = false;
        if (currentFreezeMove != null)
            StopCoroutine(currentFreezeMove);
        currentFreezeMove = StartCoroutine(Timer(FreeMove, time));
    }
    public void FreeMove()
    {
        if (currentFreezeMove != null)
            StopCoroutine(currentFreezeMove);
        canMove = true;
    }

    /// <summary>
    /// 在每物理帧最后加上附着物体的速度
    /// </summary>
    void AttachedMovement()
    {
        if (state.Equals(StateEnum.Ground))
        {
            currentMovementObject = underMovementObject;
        }

        if (currentMovementObject)
        {
            rb.velocity += currentMovementObject.rb.velocity;
        }
        currentMovementObject = null;
    }
}


/// <summary>
/// TODO 自定义编辑器
/// </summary>
//[CustomEditor(typeof(PlayerInput)),CanEditMultipleObjects]
//public class PlayerInputEditor : Editor
//{
//    public static SerializedProperty wallGrab;
//    public static SerializedProperty wallSlice;
//    public static SerializedProperty wallJump;
//    public static SerializedProperty horizontalDash;
//    public static SerializedProperty verticalDash;
//    public static SerializedProperty octDash;

//    void OnEnable()
//    {

//        wallGrab = serializedObject.FindProperty("wallGrab");
//        wallSlice = serializedObject.FindProperty("wallGrab");
//        wallJump = serializedObject.FindProperty("wallGrab");
//        horizontalDash = serializedObject.FindProperty("wallGrab");
//        verticalDash = serializedObject.FindProperty("wallGrab");
//        octDash = serializedObject.FindProperty("wallGrab");
//    }
//    public override void OnInspectorGUI()
//    {

//        serializedObject.Update();


//        EditorGUILayout.PropertyField(wallGrab);
//        if (wallGrab.boolValue)
//        {
//            EditorGUILayout.PropertyField(wallSlice);
//            EditorGUILayout.PropertyField(wallJump);
//        }

//        EditorGUILayout.PropertyField(horizontalDash);
//        EditorGUILayout.PropertyField(verticalDash);
//        EditorGUILayout.PropertyField(octDash);

//        serializedObject.ApplyModifiedProperties();
//    }
//}



/*
 * 关于构架上的解耦：
 *  state由一个专门的状态机来解决，每帧检测运行状态判断state，并监听状态转换
 *  除此之外player自己的动作只需要管自己的，并且正确地回调监听即可
 * 
 * 摩擦力处理：
 *      材质摩擦力为0，摩擦力内容自己算。摩擦力使用一个输入方向来进行，当输入方向不为运动方向，即开始减速。
 *      
 * 关于射线检测：
 *      状态机的检测是恒定的
 *      操作的额外检测，射线长度随移动速度增加而加长（如果要照顾细节，则墙跳离墙稍远的时候生效，让角色瞬移到墙上再起跳，之类需要实现）
 *      
 * 轻松跳：
 *  允许稍微提前或者滞后按跳，跳跃仍然生效
 *      地面：
 *          提前跳：即便提前，也必须在触地之后才起跳
 *          狼跳：短时间离开地面也可以跳
 *      墙跳：
 *          提前跳：TODO，没想清楚。投射碰撞体不行，会导致头顶有东西时上跳可能误触发。这个以后优化来，暂时只允许提前输入。
 *          滞后跳：无
 *      空跳：无
 *          
 */



/*
 * Debug备忘
 *  
 */
