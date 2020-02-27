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
    //public bool verticalDash;
    //public bool octDash;

    [Header("Basic Moving")]
    public float speed = 8;
    public float jumpForce = 17.5f;
    public int jumpCount = 1; // 允许跳跃次数
    public float airDrag = 1; // 空中移动能力倍率

    [Header("Wall About")]
    public float climbSpeed; // 上爬速度
    public float sliceSpeed; // 下滑速度，无体力下滑同速
    public bool isStamina; // 是否启用耐力
    public float stamina; // 耐力
    public float grabCost; // 抓墙消耗耐力per sec
    public float climbCost; // 爬墙消耗耐力per sec
    public float sliceCost; // 滑墙消耗耐力per sec ，一般设置为0
    public float wallJumpHroi; // 墙跳横向拆分

    [Header("Freeze Time")] // 禁止行动时间
    public float jumpFreezeTime; // 平地跳
    public float airJumpFreezeTime; // 空中跳
    public float onWallJumpFreezeTime; // 上墙墙跳
    public float byWallJumpFreezeTime; // 靠墙墙跳
    public float dashFreezeTime; // 冲刺


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


    [Header("Gravity Multiplier")]// 不同状态下的引力倍数
    public float jumpGravityMultiplier = 1f; // 跳跃中
    public float releaseGravityMultiplier = 2f; // 释放跳跃键的条约中
    public float dropGravityMultiplier = 2.5f; // 下落

    [Header("Ray Cast")]
    public LayerMask checkLayer;

    [Space]
    public int currentJumpCount; // 当前跳跃剩余次数基数
    public float currentStamina;

    bool jumpPressed;
    bool facingRight = true;

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
        WallGrab, // climb是grab下一状态
        WallSlice, // 当体力耗尽的抓墙
    }

    void Update()
    {
        if (Input.GetButtonDown("Jump"))
        {
            jumpPressed = true;
        }

        DrawDebugLine();
    }

    private void FixedUpdate()
    {
        StateCheck(); //必须放在第一位，每FixedUpdate先check一遍state，再根据行为（有时会参考state）修改state 
        Jump();
        GroundMovement();
        BetterGravity(); //必须放在StateCheck之后
    
    }

    void GroundMovement()
    {
        if (canMove)
        {
            float horizontalMove = Input.GetAxis("Horizontal");
            rb.velocity = new Vector2(horizontalMove * speed, rb.velocity.y);
        }
    }

    bool Jump()
    {
        if (jump)
        {
            if (isGround)
            {
                currentJumpCount = jumpCount;
                isJump = false;
            }
            if (canJump)
            {
                if (jumpPressed && isGround) // 平地跳
                {
                    isJump = true;
                    rb.velocity = new Vector2(rb.velocity.x, jumpForce);
                    currentJumpCount--;
                    jumpPressed = false;

                    StateChange(StateEnum.Jump);

                    FreezeJump(jumpFreezeTime);

                    Debug.Log("jump");
                    return true;
                }
                else if (jumpPressed && (/*isGrab || isSlice || */byWall) /*&& wallJump*/) // 墙跳
                {
                    isJump = true;
                    if (wallSide == 0)
                    {
                        rb.velocity = new Vector2(0, jumpForce);
                    }
                    else rb.velocity = new Vector2(wallJumpHroi * wallSide, jumpForce).normalized * jumpForce;
                    jumpPressed = false;

                    StateChange(StateEnum.Jump);

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
                else if (jumpPressed && currentJumpCount > 0 && !isGround) // 空中跳
                {
                    rb.velocity = new Vector2(rb.velocity.x, jumpForce);
                    currentJumpCount--;
                    jumpPressed = false;
                    StateChange(StateEnum.Jump);
                    Debug.Log("air jump");
                    return false;
                }
                else if
                (
                    !Physics2D.Raycast(transform.position + bottomRayPosition[0], Vector2.down, rayLength * 5, checkLayer)
                    && !Physics2D.Raycast(transform.position + bottomRayPosition[1], Vector2.down, rayLength * 5, checkLayer)
                    && !Physics2D.Raycast(transform.position + bottomRayPosition[2], Vector2.down, rayLength * 5, checkLayer)
                ) // 没跳,检测使得稍微落地前一点按跳也能算跳
                {
                    jumpPressed = false;
                }
            }
        }
        return false;
    }
    /// <summary>
    /// 修改跳跃、下落途中的重力倍数
    /// </summary>
    void BetterGravity()
    {
        if (state.Equals(StateEnum.Drop))
        {
            //rb.velocity += Vector2.up * Physics2D.gravity.y * (dropGravityMultiplier - 1) * Time.fixedDeltaTime;
            rb.AddForce(Physics.gravity * rb.mass * (dropGravityMultiplier - 1));
        }
        else if (state.Equals(StateEnum.ReleaseJump))
        {
            //rb.velocity += Vector2.up * Physics2D.gravity.y * (releaseGravityMultiplier - 1) * Time.fixedDeltaTime;
            rb.AddForce(Physics.gravity * rb.mass * (releaseGravityMultiplier - 1));
        }
        else if (state.Equals(StateEnum.Jump))
        {
            //rb.velocity += Vector2.up * Physics2D.gravity.y * (jumpGravityMultiplier - 1) * Time.fixedDeltaTime;
            rb.AddForce(Physics.gravity * rb.mass * (jumpGravityMultiplier - 1));
        }
    }
    /// <summary>
    /// 检查状态
    /// </summary>
    void StateCheck()
    {
        //isGround = Physics2D.OverlapCircle(groundCheck.position, 0.1f, ground);
        GroundCheck();

        if (isGround) 
        {
            StateChange(StateEnum.Ground);
        }
        else if (rb.velocity.y < 0 && !isGround)
        {
            StateChange(StateEnum.Drop);
        }
        else if (!Input.GetButton("Jump") && state.Equals(StateEnum.Jump))
        {
            StateChange(StateEnum.ReleaseJump);
        }
        else if (rb.velocity.y > 0 && Input.GetButton("Jump"))
        {
            StateChange(StateEnum.Jump);
        }
    }
    /// <summary>
    /// 状态转换
    /// </summary>
    /// <param name="para"></param>
    void StateChange(StateEnum para)
    {
        state = para;
    }

    public Vector3[] leftRayPositin;
    public Vector3[] rightRayPosition;
    public Vector3[] bottomRayPosition;
    public float rayLength;
    //RaycastHit2D[][] leftRayHits;
    //RaycastHit2D[][] rightRayHits;
    //RaycastHit2D[][] bottomRayHits;
    public ContactFilter2D contactFilter;

    //地面检测，使用三根射线
    void GroundCheck()
    {
        bool leftH = Physics2D.Raycast(transform.position + leftRayPositin[0], Vector2.left, rayLength, checkLayer);
        bool leftM = Physics2D.Raycast(transform.position + leftRayPositin[1], Vector2.left, rayLength, checkLayer);
        bool leftL = Physics2D.Raycast(transform.position + leftRayPositin[2], Vector2.left, rayLength, checkLayer);
        bool rightH = Physics2D.Raycast(transform.position + rightRayPosition[0], Vector2.right, rayLength, checkLayer);
        bool rightM = Physics2D.Raycast(transform.position + rightRayPosition[1], Vector2.right, rayLength, checkLayer);
        bool rightL = Physics2D.Raycast(transform.position + rightRayPosition[2], Vector2.right, rayLength, checkLayer);
        bool bottomL = Physics2D.Raycast(transform.position + bottomRayPosition[0], Vector2.down, rayLength, checkLayer);
        bool bottomM = Physics2D.Raycast(transform.position + bottomRayPosition[1], Vector2.down, rayLength, checkLayer);
        bool bottomR = Physics2D.Raycast(transform.position + bottomRayPosition[2], Vector2.down, rayLength, checkLayer);
       
        if (bottomM || bottomL || bottomR) //着地
        {
            isGround = true;
        }
        else isGround = false;
        if (leftM || leftL || leftH || rightM || rightL || rightH)
        {
            byWall = true;
            wallSide = WallSideJudge(leftM, leftL, leftH, rightM, rightL, rightH);

        }
        else
        {
            byWall = false;
            wallSide = 0;
        }
    }
    /// <summary>
    /// 绘制检测线
    /// </summary>
    void DrawDebugLine()
    {
        Debug.DrawLine(transform.position + leftRayPositin[0], transform.position + leftRayPositin[0] + Vector3.left * rayLength, Color.yellow);
        Debug.DrawLine(transform.position + leftRayPositin[1], transform.position + leftRayPositin[1] + Vector3.left * rayLength, Color.yellow);
        Debug.DrawLine(transform.position + leftRayPositin[2], transform.position + leftRayPositin[2] + Vector3.left * rayLength, Color.yellow);
        Debug.DrawLine(transform.position + rightRayPosition[0], transform.position + rightRayPosition[0] + Vector3.right * rayLength, Color.yellow);
        Debug.DrawLine(transform.position + rightRayPosition[1], transform.position + rightRayPosition[1] + Vector3.right * rayLength, Color.yellow);
        Debug.DrawLine(transform.position + rightRayPosition[2], transform.position + rightRayPosition[2] + Vector3.right * rayLength, Color.yellow);

        Debug.DrawLine(transform.position + bottomRayPosition[0], transform.position + bottomRayPosition[0] + Vector3.down * rayLength * 5, Color.red);
        Debug.DrawLine(transform.position + bottomRayPosition[1], transform.position + bottomRayPosition[1] + Vector3.down * rayLength * 5, Color.red);
        Debug.DrawLine(transform.position + bottomRayPosition[2], transform.position + bottomRayPosition[2] + Vector3.down * rayLength * 5, Color.red);

        Debug.DrawLine(transform.position + bottomRayPosition[0], transform.position + bottomRayPosition[0] + Vector3.down * rayLength, Color.blue);
        Debug.DrawLine(transform.position + bottomRayPosition[1], transform.position + bottomRayPosition[1] + Vector3.down * rayLength, Color.blue);
        Debug.DrawLine(transform.position + bottomRayPosition[2], transform.position + bottomRayPosition[2] + Vector3.down * rayLength, Color.blue);
    }

    /// <summary>
    /// 判断墙的方向
    /// </summary>
    int WallSideJudge(bool l1, bool l2, bool l3,bool r1, bool r2, bool r3)
    {
        if ((l1 || l2 || l3)&&(!r1 && !r2 && !r3))
        {
            return 1;
        }
        else if((r1 || r2 || r3) && (!l1 && !l2 && !l3))
        {
            return -1;
        }
        return 0;
    }
    /// <summary>
    /// 管理面朝方向
    /// </summary>
    void FacingDir()
    {
        // 旧换向方法
        //if (isFlixed ? (rb.velocity.x > 0.01f) : (rb.velocity.x < 0.01f))
        //{
        //    isFlixed = !isFlixed;
        //    transform.localScale = new Vector3(-transform.localScale.x, transform.localScale.y, transform.localScale.z);
        //}
    }
    /// <summary>
    /// 倒计时工具
    /// </summary>
    private IEnumerator Timer(FreeAction freeAction,float time)
    {
        yield return 0;
        float t = time;
        while (t > 0)
        {
            yield return 1;
            t -= Time.deltaTime;
        }
        freeAction();
        yield return null;
    }

    delegate void FreeAction();
    /// <summary>
    /// 禁止跳跃，我也不知道有啥用……反正先写了
    /// </summary>
    /// <param name="time"></param>
    void FreezeJump(float time)
    {
        canJump = false;
        StartCoroutine(Timer(FreeJump,time));
    }
    public void FreeJump()
    {
        canJump = true;
    }
    /// <summary>
    /// 禁止移动
    /// </summary>
    /// <param name="time"></param>
    void FreezeMove(float time)
    {
        canMove = false;
        StartCoroutine(Timer(FreeMove, time));
    }
    public void FreeMove()
    {
        canMove = true;
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
     *  state由一个专门的状态机来解决，它在各处放置监听，根据监听的内容自己决定状态。即：定期检查状态+监听触发检查
     *  除此之外player自己的动作只需要管自己的，并且正确地回调监听即可
     *  目前没有这么写，现在的结构已经隐隐有些混乱的苗头了……
     * 
     * 摩擦力处理：
     *      材质摩擦力为0，摩擦力内容自己算。这样的话水平摩擦力保持恒定，纵向摩擦力用来爬墙。
     *      目前暂时不使用水平摩擦力。
     *      如果之后wallgrab以后会出现改velocity也不能下滑的话，就必须这么做。
     *      
     * 关于射线检测：
     *      状态机的检测是恒定的
     *      操作的额外检测，射线长度随移动速度增加而加长（如果要照顾细节，则墙跳离墙稍远的时候生效，让角色瞬移到墙上再起跳，之类需要实现）
     */