using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class CameraMove : MonoBehaviour
{
    public float speed = 1.0f;
    public float rotSpeed = 1.0f;
    public float sprintMultiplier = 1.0f;
    Vector3 movement = Vector3.zero;
    float pitch;
    float yaw;

    float newPitch;
    float newYaw;


    bool fpCamMode = true;//first person cam mode

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

        HandleInput();
        TransformObj();

        Cursor.lockState = fpCamMode ? CursorLockMode.Locked : CursorLockMode.None;
    }


    void HandleInput()
    {
        //UI Mode
            fpCamMode = Input.GetMouseButton(1);

        newYaw = 0.0f;
        newPitch = 0.0f;
        movement = Vector3.zero;

        if (fpCamMode)
        {
            //Rotation
            newYaw = Input.GetAxis("Mouse X");//Pitch
            newPitch = Input.GetAxis("Mouse Y");//Yaw

    

            //Movement
            float up = 0.0f;
            if (Input.GetKey(KeyCode.Q))
                up += -1.0f;
            if (Input.GetKey(KeyCode.E))
                up += 1.0f;

            movement = new Vector3(Input.GetAxis("Horizontal"), up, Input.GetAxis("Vertical"));

            if (Input.GetKey(KeyCode.LeftShift))
                movement *= sprintMultiplier;
        }

    }


    void TransformObj()
    {
        pitch -= newPitch * rotSpeed * Time.deltaTime;
        yaw += newYaw * rotSpeed * Time.deltaTime;

        gameObject.transform.eulerAngles = new Vector3(pitch, yaw, 0.0f) ;

        gameObject.transform.position += gameObject.transform.rotation * movement * speed * Time.deltaTime;

    }
}
