using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using System.IO;
using UnityEngine.UI;

/// 메인 코드 ///

public class FiveDoF : MonoBehaviour
{
    public static extern void PlaneDetect(int index, int fx, int fy);

    ////////////////////// 변수 /////////////////////////

    GameObject LFS = null; // 3D 구의 LF Space -> 이 LFS 에 360 이미지 mapping 하고 카메라로 보는 방식으로 VR 구현
    Camera MainCam = null; // 유니티 player (user) view 생성하는 메인캠
    Vector3 v = new Vector3(0.1f, 0.0f, 0.0f); // 포지션 좌우 이동시 피연산 벡터
    int currentPos = 50; // 디폴트 포지션 (0050.jpg) -> 처음 이미지 로딩할 때 불러오는 이미지

    // 이미지, Depth 디렉토리 경로
    string directoryName = "SAMPLE/"; // 이미지 경로
    string depth_directoryName = "SAMPLE_depth/"; // depth 경로
    int maxImgIdx = 631;   // 이미지 마지막 index (631.jpg) -> refer to "directoryName" folder

    GameObject depth_mesh; // Depth 로 생성되는 mesh (배경) object
    GameObject AR_object; // Depth mesh 와 interact 되는 AR_object (농구공)

    // Depth_mesh object 구현을 위한 변수들 (유니티)
    MeshFilter mesh_filter; // 메쉬를 저장하는 컴포넌트. 메쉬에 디자인을 덧씌우는 역할 - 메쉬 : 3D 모델의 3차원 형상정보
    MeshRenderer Mesh_render; // 메쉬 렌더링을 하는 컴포넌트
    Mesh mesh; 
    MeshCollider mesh_collider; // 메쉬가 '물리적'으로 충돌하는 부분을 표현하는 컴포넌트
    Material mesh_mat; // 특정 메쉬를 얼마나 타일링하고, 표면의 질감은 어떻게 할 건지를 설정하는 컴포넌트
    Vector3 worldPoint;

    // AR_object의 좌표 및 방향을 저장하는 전역변수
    Vector3 worldPoint_depth = new Vector3(5.0f, 105.0f, 5.0f);
    Vector3 worldRot_depth;
    Vector3 AR_mesh_position;

    // Depth mesh object 좌표를 저장하는 전역변수
    Vector3 ocllusion_pos;

    //User 화면에 보이게 될 정보를 저장하는 변수
    Texture2D screenShot_image;
    Texture2D screenShot;

    int sample_number = 3; // Computational cost 때문에 mesh sampling 을 depth 3pixel 당 1 pixel 만 vertices 로 변경
    public bool nowReading_depth { get; private set; } // Depth 로 mesh 변경 flag

    Vector3[] sub_vertices; // depth_mesh를 저장하는 꼭짓점 변수

    float timer;
    int waiting_time;
    bool scene;
    int index = 0;

    Quaternion originalRotation; // 회전량 
    public GameObject[] brick; 
    public GameObject prefab; 
    GameObject LFD = null; 
    GameObject BG = null; 
    GameObject AR = null; 
    int current_left = 255; 
    int current_right = 255; 
    public bool nowCapturing { get; private set; } 
    public bool nowDetecting_plane { get; private set; } 
    Vector3 originalPosition; 
    SphereCollider sphere;

    //   PointCloudExporter.PointCloudGenerator pt_generator = new PointCloudExporter.PointCloudGenerator();

    public void set_GameObject(int renderSizeX, int renderSizeY, int sample_number) // 초기 object
    {
        //brick = new GameObject[(renderSizeX*renderSizeY)/sample_number ];
        brick = new GameObject[168063];
        Debug.Log((renderSizeX * renderSizeY) / sample_number);
    }

    public void CaptureScreen(string capture_name) // Capture screen script, utilize this if needed.  
    {
        // 동기화 플래그 설정
        nowCapturing = true;

        // 화면 캡쳐를 위한 코루틴 시작 - 코루틴? 스레드를 중단하지 않으면서 비동기적으로 실행되는 코드 / 비동기? 'TV를 보면서 밥을 먹는다' 와 같은 한번에 여러개의 작업을 동시에 하는 것
        Debug.Log("Capture on going");
        StartCoroutine(RenderToTexture(640, 480, capture_name));
    }

    private IEnumerator RenderToTexture(int renderSizeX, int renderSizeY, string capture_name) // Capture screen script, utilize this if needed. 
    {

        //     int targetWidth = uitextureForSave.width;
        //     int targetHeight = uitextureForSave.height;

        //RenderTexture 생성
        RenderTexture rt = new RenderTexture(renderSizeX, renderSizeY, 24);

        //RenderTexture 저장을 위한 Texture2D 생성
        Texture2D screenShot = new Texture2D(renderSizeX, renderSizeY, TextureFormat.ARGB32, false);

        // 카메라에 RenderTexture 할당
        MainCam.targetTexture = rt;

        // 각 카메라가 보고 있는 화면을 렌더링
        MainCam.Render();

        // read 하기 위해 렌더링 된 RenderTexture를 RenderTexture.active에 설정
        RenderTexture.active = rt;

        // RenderTexture.active에 설정된 RenderTexture를 read 합니다.
        screenShot.ReadPixels(new Rect(0, 0, renderSizeX, renderSizeY), 0, 0);
        screenShot.Apply();

        // File로 출력
        byte[] bytes = screenShot.EncodeToPNG();
        System.IO.File.WriteAllBytes(capture_name, bytes);

        // 사용한 것들 해제
        RenderTexture.active = null;
        MainCam.targetTexture = null;
        //        _uiCamera.targetTexture = null;
        Destroy(rt);

        // 동기화 플래그 해제
        nowCapturing = false;

        yield return 0;
    }

    private static System.Diagnostics.Stopwatch sw_ReadImg = new System.Diagnostics.Stopwatch();

    public void Do_PlaneDetect(string capture_name) 
    {
        // 동기화 플래그 설정
        nowDetecting_plane = true;

        // 화면 캡쳐를 위한 코루틴 시작
        Debug.Log("Capture on going");
        StartCoroutine(RenderToPlane(640, 480, capture_name));
    }

    private IEnumerator RenderToPlane(int renderSizeX, int renderSizeY, string capture_name) 
    {
        yield return new WaitForEndOfFrame();

        //RenderTexture 생성
        RenderTexture rt = new RenderTexture(renderSizeX, renderSizeY, 24);

        //RenderTexture 저장을 위한 Texture2D 생성
        Texture2D screenShot = new Texture2D(renderSizeX, renderSizeY, TextureFormat.ARGB32, false);

        // 카메라에 RenderTexture 할당
        MainCam.targetTexture = rt;

        // 각 카메라가 보고 있는 화면을 렌더링 합니다.
        MainCam.Render();

        // read 하기 위해, 렌더링 된 RenderTexture를 RenderTexture.active에 설정
        RenderTexture.active = rt;

        // RenderTexture.active에 설정된 RenderTexture를 read 합니다.
        screenShot.ReadPixels(new Rect(0, 0, renderSizeX, renderSizeY), 0, 0);
        screenShot.Apply();

        // 캡쳐가 완료 되었습니다.
        // 이제 캡쳐된 Texture2D 를 가지고 원하는 행동을 하면 됩니다.

        // 저는 UITexture 쪽에 넣어두었다가, 공유하는 쪽에서 꺼내서 사용하였습니다.
        //       uitextureForSave.mainTexture = screenShot;

        // File로 쓰고 싶다면 아래처럼 하면 됩니다.
        byte[] bytes = screenShot.EncodeToPNG();


        // 사용한 것들 해제
        RenderTexture.active = null;
        MainCam.targetTexture = null;

        Destroy(rt);

        // 동기화 플래그 해제
        nowDetecting_plane = false;

        yield return 0;
    }

    public void Do_ReadDepth(bool start)
    {
        nowReading_depth = true; // bool

        if (start)
        {
            StartCoroutine(ReadDepth(Screen.width, Screen.height, start)); // Depth 읽기 코루틴 시작 (start 될 시)
        }
        else
        {
            StartCoroutine(Move_depth(Screen.width, Screen.height)); // 그게 아니라면 depth 이동
        }

    } // Depth 읽고 mesh 로 변경하는 wrap code

    

    private IEnumerator ReadDepth(int renderSizeX, int renderSizeY, bool Start) // depth 값을 읽고 mesh depth를 초기화하는 함수
    {
        yield return new WaitForEndOfFrame();

        int num_vertices = (renderSizeX / sample_number) * (renderSizeY / sample_number); // 꼭짓점 개수 계산식
        mesh = new Mesh();
        depth_mesh = GameObject.Find("Depth_mesh");
        Mesh_render = depth_mesh.GetComponent<MeshRenderer>();
        mesh_filter = depth_mesh.AddComponent<MeshFilter>(); // 왜 여기만 Get이 아니고 Add인지?
        mesh_collider = depth_mesh.GetComponent<MeshCollider>();


        sub_vertices = new Vector3[num_vertices];
        int[,] TwoD_indices = new int[renderSizeX / sample_number, renderSizeY / sample_number]; // indices ?

        mesh_mat = new Material(Shader.Find("VideoPlaneNoLight"));

        mesh_mat.enableInstancing = true;



        directoryName = "SAMPLE_depth/";
        LFS.GetComponent<Renderer>().material.mainTexture = Resources.Load(directoryName + currentPos.ToString("D4")) as Texture2D;
        RenderTexture rt = new RenderTexture(renderSizeX, renderSizeY, 24);

        Texture2D screenShot = new Texture2D(renderSizeX, renderSizeY, TextureFormat.ARGB32, false); // ARGB32 Texture Import format

        MainCam.targetTexture = rt;
        MainCam.Render();

        RenderTexture.active = rt;

        screenShot.ReadPixels(new Rect(0, 0, renderSizeX, renderSizeY), 0, 0);
        screenShot.Apply();

        mesh_mat.SetTexture("texture", screenShot);
        Mesh_render.GetComponent<MeshRenderer>().material = mesh_mat;

        if (Start)
        {

            for (int x = 0; x < renderSizeX / sample_number; x++)
            {
                for (int y = 0; y < renderSizeY / sample_number; y++)
                {

                    ocllusion_pos.x = sample_number * x;
                    ocllusion_pos.y = sample_number * y;
                    ocllusion_pos.z = (screenShot.GetPixel(sample_number * x, sample_number * y).grayscale);

                    worldPoint = Camera.main.ScreenToWorldPoint(ocllusion_pos);
                    sub_vertices[(renderSizeY / sample_number) * (x) + (y)] = worldPoint;

                }
            }

            // 삼각형 메쉬 계산 변수
            for (int row = 0; row < renderSizeX / sample_number; row++)
            {
                for (int col = 0; col < renderSizeY / sample_number; col++)
                {
                    TwoD_indices[row, col] = (renderSizeY / sample_number) * row + col;
                }

            }
            ////////////// mesh initialization //////////////
            mesh.vertices = sub_vertices;

            List<int> triangles = new List<int>();
            for (int row = 1; row < (renderSizeX / sample_number) - 1; row++)
            {
                for (int col = 1; col < (renderSizeY / sample_number) - 1; col++)
                {
                    triangles.Add(TwoD_indices[row, col]);
                    triangles.Add(TwoD_indices[row + 1, col]);
                    triangles.Add(TwoD_indices[row, col + 1]);


                    triangles.Add(TwoD_indices[row, col]);
                    triangles.Add(TwoD_indices[row - 1, col]);
                    triangles.Add(TwoD_indices[row, col - 1]);

                }
            }

            triangles.Reverse();

            mesh.triangles = triangles.ToArray();
            Vector2[] uv = new Vector2[num_vertices];


            for (int row = 0; row < (renderSizeX / sample_number); row++)
            {
                for (int col = 0; col < (renderSizeY / sample_number); col++)
                {
                    float render_x = (float)renderSizeX / (float)sample_number;
                    float render_y = (float)renderSizeY / (float)sample_number;

                    uv[(renderSizeY / sample_number) * row + col] = new Vector2((float)((float)row / render_x), (float)((float)col / render_y));

                }
            }

            mesh.uv = uv;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            for (int n = 0; n < mesh.normals.Length; n++)
            {
                mesh.normals[n] = -mesh.normals[n];
            }
            mesh_filter.mesh = mesh;
        }
        mesh_collider.sharedMesh = mesh;
        mesh_collider.convex = true;
        //     mesh_collider.isTrigger = true;

        RenderTexture.active = null;
        MainCam.targetTexture = null;

        Destroy(rt);

        // 동기화 플래그 해제
        nowReading_depth = false;

        yield return 0;
    } // Depth 읽고 mesh initialize 하는 함수

    // 사용자의 view에 따라 메쉬 수정
    private IEnumerator Move_depth(int renderSizeX, int renderSizeY)
    {

        UnityEngine.Object.Destroy(screenShot);
        UnityEngine.Object.Destroy(screenShot_image);
        mesh_collider.convex = false;

        int num_vertices = (renderSizeX / sample_number) * (renderSizeY / sample_number);

        directoryName = "SAMPLE_depth/";
        LFS.GetComponent<Renderer>().material.mainTexture = Resources.Load(directoryName + currentPos.ToString("D4")) as Texture2D;
        RenderTexture rt = new RenderTexture(renderSizeX, renderSizeY, 24);

        screenShot = new Texture2D(renderSizeX, renderSizeY, TextureFormat.ARGB32, false);

        MainCam.targetTexture = rt;
        MainCam.Render();
        RenderTexture.active = rt;

        screenShot.ReadPixels(new Rect(0, 0, renderSizeX, renderSizeY), 0, 0);
        screenShot.Apply();


        directoryName = "SAMPLE/";
        LFS.GetComponent<Renderer>().material.mainTexture = Resources.Load(directoryName + currentPos.ToString("D4")) as Texture2D;
        RenderTexture rt_image = new RenderTexture(renderSizeX, renderSizeY, 24);

        screenShot_image = new Texture2D(renderSizeX, renderSizeY, TextureFormat.ARGB32, false);


        MainCam.targetTexture = rt_image;


        MainCam.Render();

        RenderTexture.active = rt_image;

        screenShot_image.ReadPixels(new Rect(0, 0, renderSizeX, renderSizeY), 0, 0);
        screenShot_image.Apply();

        Mesh_render.material.SetTexture("_MainTex", screenShot);


        // Make depth_mesh transparent;; 
        Mesh_render.material.SetColor("_Color", Color.clear);

        for (int x = 0; x < renderSizeX / sample_number; x++)
        {
            for (int y = 0; y < renderSizeY / sample_number; y++)
            {
                ocllusion_pos.x = x * sample_number;
                ocllusion_pos.y = y * sample_number;
                ocllusion_pos.z = (1 / (screenShot.GetPixel(sample_number * x, sample_number * y).grayscale));   

                worldPoint = Camera.main.ScreenToWorldPoint(ocllusion_pos);
                worldRot_depth = MainCam.transform.forward;

                sub_vertices[(renderSizeY / sample_number) * (x) + (y)] = worldPoint;
            }
        }

        mesh.vertices = sub_vertices;
        mesh_filter.mesh = mesh;

        RenderTexture.active = null;
        MainCam.targetTexture = null;

        Destroy(rt);
        Destroy(rt_image);

        // 동기화 플래그 해제
        nowReading_depth = false;

        yield return 0;
    } // Init 된 mesh 를 user-view 에 해당하는 depth 정보 읽어와서 매 프레임마다 변경하는 함수

    // 정해진 좌표로 AR_object reset
    void reset_object(GameObject AR)
    {
        AR.transform.position = AR_mesh_position;
    }

    // 카메라가 보는 방향으로 AR_object throw
    void throw_object(GameObject AR, int speed)
    {
        AR.transform.position = worldPoint_depth;

        AR.GetComponent<Rigidbody>().velocity = worldRot_depth * speed;
        AR.GetComponent<Rigidbody>().useGravity = true; // 중력 적용

    }


    void Start() 
    {

        MainCam = GameObject.FindWithTag("MainCamera").GetComponent<Camera>(); // 메인캠 접근 위해 카메라를 찾음

        // Init AR_object                                  
        AR_object = GameObject.Find("ARobject_mesh");
        AR_mesh_position = AR_object.transform.position;

        originalPosition = MainCam.transform.position;
        originalRotation = MainCam.transform.localRotation;

        LFS = GameObject.Find("LFSpace"); // LF
        LFS.GetComponent<Renderer>().enabled = true;

        var color = LFS.GetComponent<Renderer>().material.color;
        var newColor = new Color(color.r, color.g, color.b, 0.5f);
        LFS.GetComponent<Renderer>().material.color = newColor;

        LFS.GetComponent<Renderer>().material.shader = Shader.Find("Unlit/Pano360Shader"); // 구에 입혀진 텍스쳐를 렌더링하기 위해 쉐이더 로드
        currentPos = 1;

        LFS.GetComponent<Renderer>().material.mainTexture = Resources.Load(directoryName + currentPos.ToString("D4")) as Texture2D; //구에 입힐 최초 이미지

        Do_ReadDepth(true); // Mesh flag on

        sw_ReadImg.Reset();
    }

    void Update()
    {
        sw_ReadImg.Reset();
        sw_ReadImg.Start();
        int index = 0;

        timer += Time.deltaTime;
        if (timer > 0.1)
        {
            Do_ReadDepth(false);
            timer = 0;
        }

        if (Input.GetKeyUp(KeyCode.Y)) // 화면 캡쳐 
        {

            string fmt = "00000";
            string file_path = "C:/Users/yuniw/Desktop/Unity5DoF/data/";
            string filename = "img_" + index.ToString() + ".png";

            directoryName = "SAMPLE/";
            LFS.GetComponent<Renderer>().material.mainTexture = Resources.Load(directoryName + currentPos.ToString("D4")) as Texture2D;
            CaptureScreen(file_path + filename);

            string filename_depth = "depth_" + index.ToString() + ".png";

            directoryName = "SAMPLE_depth/";
            LFS.GetComponent<Renderer>().material.mainTexture = Resources.Load(directoryName + currentPos.ToString("D4")) as Texture2D;
            CaptureScreen(file_path + filename_depth);

            int fx = 383;
            //            PlaneDetect(index,fx, fx);

        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            Do_ReadDepth(false);
        }

        if (Input.GetKey(KeyCode.C)) //Not used
        {
            directoryName = "SAMPLE/";
            LFS.GetComponent<Renderer>().material.mainTexture = Resources.Load(directoryName + currentPos.ToString("D4")) as Texture2D;
        }

        if (Input.GetKey(KeyCode.V))  // 누르고 있으면 DEPTH view 확인
        {
            directoryName = "SAMPLE_depth/";
            LFS.GetComponent<Renderer>().material.mainTexture = Resources.Load(directoryName + currentPos.ToString("D4")) as Texture2D;
        }

        if (Input.GetKey(KeyCode.O)) // reset AR object to default position
        {
            reset_object(AR_object);
        }

        if (Input.GetKey(KeyCode.I)) // Throw AR object
        {
            throw_object(AR_object, 20);
        }

        if (Input.GetKey(KeyCode.D)) // 카메라 우측 이동
        {
            currentPos = currentPos + 5;
            current_left = currentPos - 5;
            current_right = currentPos + 5;

            if (currentPos > maxImgIdx)
            {
                currentPos = maxImgIdx;
            }
            LFS.GetComponent<Renderer>().material.mainTexture = Resources.Load(directoryName + currentPos.ToString("D4")) as Texture2D;
        }

        if (Input.GetKey(KeyCode.A)) // 카메라 좌측 이동
        {
            currentPos = currentPos - 5;
            current_left = currentPos - 5;
            current_right = currentPos + 5;

            if (currentPos < 1)
            {
                currentPos = 1;
            }
            LFS.GetComponent<Renderer>().material.mainTexture = Resources.Load(directoryName + currentPos.ToString("D4")) as Texture2D;
        }
        sw_ReadImg.Stop();
    }

    public void LFS_setup(GameObject LFS_temp, string object_name, int current_pos) // LF Space 설정 코드
    {
        LFS_temp = GameObject.Find(object_name); // LF
        LFS_temp.GetComponent<Renderer>().enabled = true;
        LFS_temp.GetComponent<Renderer>().material.shader = Shader.Find("Unlit/Pano360Shader"); // 구에 입혀진 텍스쳐를 렌더링하기 위해 쉐이더 로드
        LFS_temp.GetComponent<Renderer>().material.mainTexture = Resources.Load(directoryName + current_pos.ToString("D4")) as Texture2D; //구에 입힐 최초 이미지
        if (LFS_temp)
        {
            Debug.Log("no" + object_name + "found");
        }

    }
}
