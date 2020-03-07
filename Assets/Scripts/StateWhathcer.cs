using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StateWhathcer : MonoBehaviour
{
    public Text states;
    public Player PI;

    private void Start()
    {
        PI = GameObject.Find("Player").GetComponent<Player>();
    }
    private void Update()
    {
        string _text =
            "Velocity: " + PI.rb.velocity.ToString() + "\n" +
            "Is Ground: " + PI.isGround.ToString() + "\n" +
            "Is Jump: " + PI.isJump.ToString() + "\n" +
            "Jump Count: " + PI.currentJumpCount.ToString() + "\n" +
            "State: " + PI.state.ToString() + "\n" +
            "Stamina: " + PI.currentStamina.ToString() + "\n";
        states.text = _text;
    }


}
