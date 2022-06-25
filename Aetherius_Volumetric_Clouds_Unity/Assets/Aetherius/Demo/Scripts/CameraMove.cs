using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aetherius.Demo
{

    public class CameraMove : MonoBehaviour
    {
        float minAccel = 0.01f;
        float maxAccel = 2.0f;
        float accel = 1.0f;

        float defaultSpeed = 10.0f;
        public float speed = 0.0f;
        float rotSpeed = 2.5f;
        float sprintMultiplier = 3.0f;
        Vector3 movementDir = Vector3.zero;
        Vector3 lastInputDir = Vector3.zero;
        float pitch;
        float yaw;

        float newPitch;
        float newYaw;

        [HideInInspector]
        public bool enabledControl = true;
        bool fpCamMode = true;//first person cam mode


        Vector3 camOrigin = new Vector3(0.0f, 2.0f, 0.0f);



        // Start is called before the first frame update
        void Start()
        {
            speed = 0;
            SetPitchYawPos(pitch, yaw, camOrigin);
        }

        // Update is called once per frame
        void Update()
        {

            HandleInput();

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

            bool isInputingMovement = false;


            if (Input.GetKey(KeyCode.F))
            {
                speed = 0;
                gameObject.transform.position = camOrigin;
                movementDir = Vector3.zero;
            }


            if (fpCamMode)
            {
                //Rotation
                newYaw = Input.GetAxis("Mouse X");//Pitch
                newPitch = Input.GetAxis("Mouse Y");//Yaw


                float wheelAxis = Input.GetAxis("Mouse ScrollWheel");
                if (wheelAxis < 0.0f)
                {
                    accel -= accel * Mathf.Abs(wheelAxis) * 1.5f;
                }
                else if (wheelAxis > 0.0f)
                {
                    accel += accel * Mathf.Abs(wheelAxis) * 1.5f;
                }

                accel = Mathf.Clamp(accel, minAccel, maxAccel);


                pitch -= newPitch * rotSpeed;
                yaw += newYaw * rotSpeed;

                gameObject.transform.eulerAngles = new Vector3(pitch, yaw, 0.0f);




                //Movement
                float up = 0.0f;
                if (Input.GetKey(KeyCode.Q))
                    up += -1.0f;
                if (Input.GetKey(KeyCode.E))
                    up += 1.0f;

                Vector3 newInputDir = new Vector3(Input.GetAxis("Horizontal"), up, Input.GetAxis("Vertical"));

                if (newInputDir.sqrMagnitude > 0.0f)
                {
                    isInputingMovement = true;
                    lastInputDir = newInputDir.normalized;

                    speed += 0.2f*speed * accel * Time.deltaTime * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1.0f);
                    speed = Mathf.Max(speed, defaultSpeed);          
                   
                }

            }


            movementDir = Vector3.Lerp(movementDir, lastInputDir,5.0f* Time.deltaTime);

            if (!isInputingMovement)
            {

                speed -= speed* Mathf.Clamp01(20.0f*Time.deltaTime);

                if (speed < defaultSpeed)
                {
                    movementDir = Vector3.zero;
                    lastInputDir = Vector3.zero;
                    speed = 0.0f;
                }
            }

           

            gameObject.transform.position += gameObject.transform.rotation * movementDir.normalized * speed * Time.deltaTime;

        }

        public void SetPitchYaw(float pitch, float yaw)
        {
            this.pitch = pitch;
            this.yaw = yaw;

            gameObject.transform.eulerAngles = new Vector3(pitch, yaw, 0.0f);
        }

        public void SetPitchYawPos(float pitch, float yaw, Vector3 pos)
        {
            SetPitchYaw(pitch, yaw);

            movementDir = Vector3.zero;
            speed = defaultSpeed;
            gameObject.transform.position = pos;
        }

    }

}