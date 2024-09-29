using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private Rigidbody rb;

    [SerializeField] private float forwardForce = 500f;
    [SerializeField] private float sidewaysForce = 500f;
    void FixedUpdate()
    {
        if (Input.GetKey("w"))
        {
            rb.AddForce(0, 0, forwardForce * Time.deltaTime);
        }
        
        if (Input.GetKey("s"))
        {
            rb.AddForce(0, 0, -forwardForce * Time.deltaTime);
        }

        if (Input.GetKey("a"))
        {
            rb.AddForce(sidewaysForce  * Time.deltaTime, 0, 0);
        }
        
        if (Input.GetKey("d"))
        {
            rb.AddForce(-sidewaysForce  * Time.deltaTime, 0, 0);
        }
        
    }
}
