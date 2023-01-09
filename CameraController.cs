using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    float sensitivityX = 15F; // float 값으로 마우스 감도 설정
    float sensitivityY = 15F;

    // float minimumX = -340F;
    // float maximumX = 340F;  - 왜 X의 최소 최대값은 설정하지 않았을까? 360도 돌아야해서?

    float minimumY = -60F;
    float maximumY = 60F;

    private static float rotationX = 0F;
    float rotationY = 0F;
    Vector2 lastAxis;  // vector2는 2차원. 아마도 카메라는 상하좌우로 움직이니까 2차원 벡터로 설정한 듯 보임.

    Quaternion originalRotation; // 회전량 선언 

    public static float getRotationY()  // 왜 getRotation"Y" 인데 return 값은 rotationX 인지?
    {
        return rotationX;
    }

    public static float ClampAngle(float angle, float min, float max) //ClampAngle - angle 고정
    {
        if (angle < -360F)
            angle += 360F;
        if (angle > 360F)
            angle -= 360F;
        return Mathf.Clamp(angle, min, max); // 최소와 최대 범위 사이에서 값을 고정 (최소~최대 값을 설정해서 범위 이외의 값을 넘지 않도록 함)
    }

    
    void Start()
    {
        lastAxis = new Vector2(Input.mousePosition.x, Input.mousePosition.y); // 마우스 위치를 기반으로 생성된 2차원 벡터를 axis로 입력
        Cursor.lockState = CursorLockMode.Locked; // 마우스 커서를 중앙 좌표에 고정
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.S))
        {
            Cursor.lockState = CursorLockMode.None; // 마우스 커서 정상 상태, 커서가 화면에 표시됨.
            Debug.Log("shiftClicked"); // shiftClicked? 아마도 s를 입력해서 마우스 커서가 shift 될 수 있음을 표현한 것 같은데 그냥 sClicked가 낫지 않았을까 ? -> 적용 안됨.
        }
        if (Input.GetKeyDown(KeyCode.LeftControl)) Cursor.lockState = CursorLockMode.Locked; // 왼쪽 컨트롤 키를 누르면 마우스 커서 고정. -> 적용 안됨.

        Vector2 position = mouse_position(); // position -> 오브젝트 강제 이동
        rotationX = position.x * sensitivityX;
        rotationY = position.y * sensitivityY;

        rotationY = ClampAngle(rotationY, minimumY, maximumY);
        transform.localEulerAngles = new Vector3(-rotationY, rotationX, 0); // 회전값 입력, vector3지만 값이 0으로 표현되면 2차원이 아닐까?
        lastAxis = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
        
    }

    Vector2 mouse_position() 
    {
        Vector2 axis = new Vector2(-(lastAxis.x - Input.mousePosition.x) * 0.5f, -(lastAxis.y - Input.mousePosition.y) * 0.5f);
        // 마우스 반전을 바꾸려면? x, y 좌표 계산식 앞의 -를 삭제 -> 적용 안됨

        return axis;
    }
}
