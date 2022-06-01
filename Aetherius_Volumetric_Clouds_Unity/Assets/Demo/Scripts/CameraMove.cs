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

    [HideInInspector]
    public bool enabledControl = true;
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

        Cursor.lockState = fpCamMode && enabledControl ? CursorLockMode.Locked : CursorLockMode.None;
    }


    void HandleInput()
    {
        if (!enabledControl)
            return;

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

    public void SetPitchYaw(float pitch,float yaw)
    {
        this.pitch = pitch;
        this.yaw = yaw;
    }

    void TransformObj()
    {
        pitch -= newPitch * rotSpeed * Time.deltaTime;
        yaw += newYaw * rotSpeed * Time.deltaTime;

        gameObject.transform.eulerAngles = new Vector3(pitch, yaw, 0.0f) ;

        gameObject.transform.position += gameObject.transform.rotation * movement * speed * Time.deltaTime;

    }
}
