using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PivotPoint : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
       transform.position = new Vector3(transform.position.x, transform.position.y + -0.5f, transform.position.z);
    }
}
