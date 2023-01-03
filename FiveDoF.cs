using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using System.IO;
using UnityEngine.UI;

/// 메인 코드 ///

public class FiveDoF : MonoBehaviour
{
    /* C++ 동적 링크 라이브러리(DLL) 사용 안함*/

    //private const string NATIVE_LIBRARY_NAME = "C:/myUnity/5DOF/Assets/5DoF";
    //[DllImport(NATIVE_LIBRARY_NAME)]
    //private static extern int nativeMakeLFSpace(string dir, int height = 1024, int width = 2048);
    //
    //[DllImport(NATIVE_LIBRARY_NAME)]
    //private static extern int nativeUpdatePicture(int posX = 50); 
    //
    //// Start is called before the first frame update
    /*
        [DllImport("DLL_test")]
        public static extern void Calc_depth();
        [DllImport("DLL_test")]
        public static extern double Add(double left_name, double right_name);
        [DllImport("ConsoleApplication1")]
        private static extern double mat_test(double a);
        */
    /*
        [DllImport("RGBD_TEST")]
        public static extern void read_image();

        [DllImport("RGBD_TEST")]
        public static extern void PlaneDetect(string color_filename, string depth_filename, string output_folder);

        */

    [DllImport("RGBD_LAST")] // Not used... ? maybe ...
    public static extern void PlaneDetect(int index,int fx,int fy); // Not used

    ////////////////////// Variables /////////////////////////

    GameObject LFS = null; // 3D 구의 LF Space -> 이 LFS 에 360 이미지 mapping 하고 카메라로 보는 방식으로 VR 구현
    Camera MainCam = null; // 유니티 player (user) view 생성하는 메인캠
    Vector3 v = new Vector3(0.1f, 0.0f, 0.0f); // 포지션 좌우 이동시 피연산 벡터
    int currentPos = 50; // 디폴트 포지션 (0050.jpg) -> 처음 이미지 로딩할 때 불러오는 이미지 #

    // 이미지, Depth 디렉토리 경로
    string directoryName = "SAMPLE/"; // 이미지 경로
    string depth_directoryName = "SAMPLE_depth/"; // depth 경로
    int maxImgIdx = 631;   // 이미지 마지막 index (631.jpg) -> refer to "directoryName" folder

    GameObject depth_mesh; // Depth 로 생성되는 mesh (배경) object
    GameObject AR_object; // Depth mesh 와 interact 되는 AR_object 

    // Depth_mesh object 구현위한 variagble 들
    MeshFilter mesh_filter;
    MeshRenderer Mesh_render;
    Mesh mesh;
    MeshCollider mesh_collider;
    Material mesh_mat;
    Vector3 worldPoint;

    // AR_object 좌표 및 방향 저장하는 global variable
    Vector3 worldPoint_depth = new Vector3(5.0f, 105.0f, 5.0f); 
    Vector3 worldRot_depth;
    Vector3 AR_mesh_position;

    // Depth mesh object 좌표 저장하는 global variable
    Vector3 ocllusion_pos;

    //User 화면에 보이게 될 정보 저장하는 variable
    Texture2D screenShot_image;
    Texture2D screenShot;

    int sample_number = 3; // Computational cost 때문에 mesh sampling 을 depth 3pixel 당 1 pixel 만 vertices 로 변경
    public bool nowReading_depth { get; private set; } // Depth 로 mesh 변경 flag

    Vector3[] sub_vertices; // depth_mesh 저장하는 vertices variable

    float timer;
    int waiting_time;
    bool scene;
    int index = 0;
 
    ///////////////////////////////////////////////////////////////////////////////////

    public GameObject[] brick; // Not used
    Quaternion originalRotation; // Not used
    public GameObject prefab; // Not used
    GameObject LFD = null; // Not used
    GameObject BG = null; // Not used
    GameObject AR = null; // Not used
    int current_left = 245; // Not used
    int current_right = 255; // Not used
    public bool nowCapturing { get; private set; } // Not used
    public bool nowDetecting_plane { get; private set; } // Not used 
    Vector3 originalPosition; // Not used...? maybe
    SphereCollider sphere; // Not used ...? maybe

    //   PointCloudExporter.PointCloudGenerator pt_generator = new PointCloudExporter.PointCloudGenerator();

    public void set_GameObject(int renderSizeX, int renderSizeY, int sample_number) // Not used
    {
        //brick = new GameObject[(renderSizeX*renderSizeY)/sample_number ];
        brick = new GameObject[168063];
        Debug.Log((renderSizeX * renderSizeY) / sample_number);
    }

    public void CaptureScreen(string capture_name) // Capture screen script, utilize this if needed.  
    {
        // 동기화 플래그 설정
        nowCapturing = true;
        
        // 화면 캡쳐를 위한 코루틴 시작
        Debug.Log("Capture on going");
        StartCoroutine(RenderToTexture(640, 480,capture_name));
    }

    private IEnumerator RenderToTexture(int renderSizeX, int renderSizeY,string capture_name) // Capture screen script, utilize this if needed. 
    {

 //       int targetWidth = uitextureForSave.width;
   //     int targetHeight = uitextureForSave.height;

        //RenderTexture 생성
        RenderTexture rt = new RenderTexture(renderSizeX, renderSizeY, 24);
        //RenderTexture 저장을 위한 Texture2D 생성
        Texture2D screenShot = new Texture2D(renderSizeX, renderSizeY, TextureFormat.ARGB32, false);

        // 카메라에 RenderTexture 할당
        MainCam.targetTexture = rt;

        // 각 카메라가 보고 있는 화면을 랜더링
        MainCam.Render();

        // read 하기 위해, 랜더링 된 RenderTexture를 RenderTexture.active에 설정
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

    public void Do_PlaneDetect(string capture_name) // Not used
    {
        // 동기화 플래그 설정
        nowDetecting_plane = true;

        // 화면 캡쳐를 위한 코루틴 시작
        Debug.Log("Capture on going");
        StartCoroutine(RenderToPlane(640, 480, capture_name));
    }

    private IEnumerator RenderToPlane(int renderSizeX, int renderSizeY, string capture_name) // 구현 X 
    {
               yield return new WaitForEndOfFrame();

        //       int targetWidth = uitextureForSave.width;
        //     int targetHeight = uitextureForSave.height;

        //RenderTexture 생성
        RenderTexture rt = new RenderTexture(renderSizeX, renderSizeY, 24);
        //RenderTexture 저장을 위한 Texture2D 생성
        Texture2D screenShot = new Texture2D(renderSizeX, renderSizeY, TextureFormat.ARGB32, false);

        // 카메라에 RenderTexture 할당
        MainCam.targetTexture = rt;

        // 각 카메라가 보고 있는 화면을 랜더링 합니다.
        MainCam.Render();

        // read 하기 위해, 랜더링 된 RenderTexture를 RenderTexture.active에 설정
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
        nowReading_depth = true;
      
        
        if (start) { 
            StartCoroutine(ReadDepth(Screen.width,Screen.height,start));
        }
        else
        {
            StartCoroutine(Move_depth(Screen.width, Screen.height));
        }
        
    } // Depth 읽고 mesh 로 변경하는 wrap code

    // function that read depth value & initialize meshes
    private IEnumerator ReadDepth(int renderSizeX, int renderSizeY,bool Start)
    {
        yield return new WaitForEndOfFrame();

        int num_vertices = (renderSizeX / sample_number) * (renderSizeY / sample_number);
        mesh = new Mesh();
        depth_mesh = GameObject.Find("Depth_mesh");
        Mesh_render = depth_mesh.GetComponent<MeshRenderer>();
        mesh_filter = depth_mesh.AddComponent<MeshFilter>();
        //Mesh_render = depth_mesh.AddComponent<MeshRenderer>();
        mesh_collider = depth_mesh.GetComponent<MeshCollider>();

        
        sub_vertices = new Vector3[num_vertices];
        int[,] TwoD_indices = new int[renderSizeX / sample_number, renderSizeY / sample_number];

        mesh_mat = new Material(Shader.Find("VideoPlaneNoLight"));

        mesh_mat.enableInstancing = true;

        

        directoryName = "SAMPLE_depth/";
        LFS.GetComponent<Renderer>().material.mainTexture = Resources.Load(directoryName + currentPos.ToString("D4")) as Texture2D;
        RenderTexture rt = new RenderTexture(renderSizeX, renderSizeY, 24);        

        Texture2D screenShot = new Texture2D(renderSizeX, renderSizeY, TextureFormat.ARGB32, false);

        

        MainCam.targetTexture = rt;


        MainCam.Render();

        RenderTexture.active = rt;

        screenShot.ReadPixels(new Rect(0, 0, renderSizeX, renderSizeY), 0, 0);
        screenShot.Apply();

        mesh_mat.SetTexture("texture", screenShot);
        Mesh_render.GetComponent<MeshRenderer>().material = mesh_mat;
       
        if (Start) {

            for (int x = 0; x < renderSizeX/sample_number; x++)
             {
                 for(int y = 0; y< renderSizeY/sample_number; y++)
                {
                  
                  ocllusion_pos.x = sample_number * x;
                  ocllusion_pos.y = sample_number * y;              
                  ocllusion_pos.z = (screenShot.GetPixel(sample_number *x, sample_number *y).grayscale);

                  worldPoint = Camera.main.ScreenToWorldPoint(ocllusion_pos);
                  sub_vertices[(renderSizeY/sample_number) * (x) + (y)] = worldPoint;
          
                }
            }

            //variable to calculate the triangle mesh;
            for(int row = 0; row < renderSizeX/sample_number; row++)
            {
                for(int col = 0; col < renderSizeY/sample_number; col++)
                {
                      TwoD_indices[row,col] = (renderSizeY/sample_number) * row + col;
                }

            }
            ////////////////////////////////// mseh initialization //////////////
            mesh.vertices = sub_vertices;

            List<int> triangles = new List<int>();
            for (int row = 1; row < (renderSizeX/sample_number)-1; row++)
            {
                for (int col = 1; col < (renderSizeY/sample_number)-1; col++)
                {
                    triangles.Add(TwoD_indices[row,col]) ;
                    triangles.Add(TwoD_indices[row+1, col]);
                    triangles.Add(TwoD_indices[row, col+1]);


                    triangles.Add(TwoD_indices[row, col]);
                    triangles.Add(TwoD_indices[row -1, col]);
                    triangles.Add(TwoD_indices[row , col - 1 ]);     
                                   
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
   
    // Modify the meshes according to the user-view 
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
        Mesh_render.material.SetColor("_Color",Color.clear);
       
        for (int x = 0; x < renderSizeX / sample_number; x++)
        {
            for (int y = 0; y < renderSizeY / sample_number; y++)
            {
                ocllusion_pos.x = x * sample_number;
                ocllusion_pos.y = y * sample_number;

                ////////////////////////////////////////////////// For better interaction, proper setting is required ///////////////////////////////////////

                //                ocllusion_pos.z = (float)3 * (1 - (float)(0.6) * (screenShot.GetPixel(sample_number * x, sample_number * y).grayscale));
                //              ocllusion_pos.z = (float)8 * (1 - (float)(0.6)*(screenShot.GetPixel(sample_number * x, sample_number * y).grayscale)) - (float)3;
                ocllusion_pos.z = (1 /  (screenShot.GetPixel(sample_number * x, sample_number * y).grayscale));
//                ocllusion_pos.z = (float)20 * (1 - (float)(0.99) * (screenShot.GetPixel(sample_number * x, sample_number * y).grayscale));
//                ocllusion_pos.z = (float)10 * (1 - (float)(0.99)*(screenShot.GetPixel(sample_number * x, sample_number * y).grayscale)) ;
                //Vector3 worldPoint = ocllusion_pos;     
                worldPoint = Camera.main.ScreenToWorldPoint(ocllusion_pos);

  //              worldPoint_depth = worldPoint;
//                worldPoint_depth.z = worldPoint.z - 00.0f;
  //              worldPoint_depth.x = worldPoint.x - 3.2f;
   //            worldPoint_depth.y = worldPoint.y - 3.2f;
        //        worldPoint_depth.z = worldPoint.z + 0.0f;

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
        // AR.transform.position = worldPoint_depth;
        AR.transform.position = AR_mesh_position;
        
    }

    // 카메라가 보는 방향으로 AR_object throw
    void throw_object(GameObject AR,int speed)
    {
        AR.transform.position = worldPoint_depth;
        //        AR.GetComponent<Rigidbody>().velocity = transform.forward * speed;
        AR.GetComponent<Rigidbody>().velocity = worldRot_depth * speed;
        AR.GetComponent<Rigidbody>().useGravity = true;

    }


    void Start() // 플레이버튼 누를 시 최초 1회만 실행
    {

        MainCam = GameObject.FindWithTag("MainCamera").GetComponent<Camera>(); // 메인캠 접근 위해 카메라를 찾음
                                             
        // Init AR_object                                  
        AR_object = GameObject.Find("ARobject_mesh");
        AR_mesh_position = AR_object.transform.position;
 

        originalPosition = MainCam.transform.position;
      
        originalRotation = MainCam.transform.localRotation;

        // 
        LFS = GameObject.Find("LFSpace"); // LF
        LFS.GetComponent<Renderer>().enabled = true;

        var color = LFS.GetComponent<Renderer>().material.color;
        var newColor = new Color(color.r, color.g, color.b, 0.5f);
        LFS.GetComponent<Renderer>().material.color = newColor;

        LFS.GetComponent<Renderer>().material.shader = Shader.Find("Unlit/Pano360Shader"); // 구에 입혀진 텍스쳐를 렌더링하기 위해 쉐이더 로드
        currentPos = 1;

        LFS.GetComponent<Renderer>().material.mainTexture = Resources.Load(directoryName + currentPos.ToString("D4")) as Texture2D; //구에 입힐 최초 이미지
        
        Do_ReadDepth(true); // Mesh flag on

/*
        LFD = GameObject.Find("LFDepth"); // LF
        LFD.GetComponent<Renderer>().enabled = true;

        var color_depth = LFD.GetComponent<Renderer>().material.color;
        var newColor_depth = new Color(color_depth.r, color_depth.g, color_depth.b, 0.5f);
        LFD.GetComponent<Renderer>().material.color = newColor_depth;

        LFD.GetComponent<Renderer>().material.shader = Shader.Find("Unlit/Pano360Shader"); // 구에 입혀진 텍스쳐를 렌더링하기 위해 쉐이더 로드
        currentPos = 1;

        LFD.GetComponent<Renderer>().material.mainTexture = Resources.Load(depth_directoryName + currentPos.ToString("D4")) as Texture2D; //구에 입힐 최초 이미지
/*        
        /*      
              LFS_left = GameObject.Find("LFSpace_Left"); // LF
              LFS_left.GetComponent<Renderer>().enabled = true;
              LFS_left.GetComponent<Renderer>().material.shader = Shader.Find("Unlit/Pano360Shader"); // 구에 입혀진 텍스쳐를 렌더링하기 위해 쉐이더 로드
              current_left = currentPos - 5;
              LFS.GetComponent<Renderer>().material.mainTexture = Resources.Load(directoryName + current_left.ToString("D4")) as Texture2D; //구에 입힐 최초 이미지


              LFS_right = GameObject.Find("LFSpace_Right"); // LF
              LFS_right.GetComponent<Renderer>().enabled = true;
              LFS_right.GetComponent<Renderer>().material.shader = Shader.Find("Unlit/Pano360Shader"); // 구에 입혀진 텍스쳐를 렌더링하기 위해 쉐이더 로드
              current_right = currentPos + 5;
              LFS_right.GetComponent<Renderer>().material.mainTexture = Resources.Load(directoryName + current_right.ToString("D4")) as Texture2D; //구에 입힐 최초 이미지
              */

        sw_ReadImg.Reset();
    }

    // Update is called once per frame
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

        if (Input.GetKeyUp(KeyCode.Y)) // DO Capture screen 
        {

            string fmt = "00000";
            string file_path = "C:/Users/yuniw/Desktop/Unity5DoF/data/";
            string filename = "img_" + index.ToString()+".png";

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

        if (Input.GetKeyDown(KeyCode.P)){
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
   /*
        if (Input.GetKey(KeyCode.L)) // Not used
        {
            string file_path = "Plane_Test/";
            string filename = "img.png";
            directoryName = "SAMPLE/";
            LFS.GetComponent<Renderer>().material.mainTexture = Resources.Load(directoryName + currentPos.ToString("D4")) as Texture2D;
            CaptureScreen(file_path + filename);

            string filename2 = "depth.png";

            directoryName = "SAMPLE_depth/";
            LFS.GetComponent<Renderer>().material.mainTexture = Resources.Load(directoryName + currentPos.ToString("D4")) as Texture2D;
            CaptureScreen(file_path + filename2);
        }
        */
        /*
        if (Input.GetKeyDown(KeyCode.T)) // Not used
        {
            Debug.Log("capture_on_going");
            currentPos = 1;
            int file_index = 0;

            float maxImgIdx = 100;
            float angle = 0;
            float angle_diff = 5;
            string fmt = "00000";
            float rotAverageY = 0;
            float rotAverageX = 180;
            Quaternion yQuaternion = Quaternion.AngleAxis(rotAverageY, Vector3.left);
            Quaternion xQuaternion = Quaternion.AngleAxis(rotAverageX, Vector3.up);

            MainCam.transform.localRotation = originalRotation * xQuaternion * yQuaternion;
            originalRotation = MainCam.transform.localRotation;
            for (int i = 0; i < maxImgIdx - 1; i++) // 가로 방향
            {
         //       i = 5 * i;
                for (float j = 0; j < 1; j++) // 세로 방향 
                {
                    for (float k = 0; k < 19; k++)
                    {
                      
                        if (k < 5)
                        {
                            angle = 180 + k * angle_diff; //180~200
                        }
                        else if ((4 < k) && (k< 10))
                        {
                            angle = 180 + (9 - k) * angle_diff; // 200~180
                          

                        }
                        else if((10 <= k) && (k<=13))
                        {
                            angle = 180 - (k-9) * angle_diff; // 180~160
                        }
                        else if ((14<=k) && (k <= 18))
                        {
                            angle = 180 - (18 - k) * angle_diff; //160~180
                        }
                        Debug.Log(angle);

                        rotAverageY = 0;
                        rotAverageX = angle;
                      
                        yQuaternion = Quaternion.AngleAxis(rotAverageY, Vector3.left);
                        xQuaternion = Quaternion.AngleAxis(rotAverageX, Vector3.up);

                        MainCam.transform.localRotation = originalRotation * xQuaternion * yQuaternion;

                        Debug.Log("capture light_field");
                        //   originalPosition.x = originalPosition.x + 0.2f * i;
                        currentPos = currentPos + 5 * i;
                        LFS.GetComponent<Renderer>().material.mainTexture = Resources.Load(directoryName + currentPos.ToString("D4")) as Texture2D;
                        LFD.GetComponent<Renderer>().material.mainTexture = Resources.Load(depth_directoryName + currentPos.ToString("D4")) as Texture2D;
                        //              originalPosition.y = originalPosition.y + 0.2f * j;
                        Debug.Log(currentPos);
                        transform.localPosition = originalPosition;

                        string file_path = "VR_testset/";
                        //                  string filename = "cap" + i.ToString() + "_" + j.ToString() + ".png";  // 
                        string filename = file_index.ToString(fmt) + ".png";
                    //    string filename = file_index.ToString() + ".png";
                        CaptureScreen(file_path + filename);
                        //      GameObject.Find("LFpace").GetComponent<FiveDoF>().CaptureScreen(filename);

                        currentPos = currentPos - 5 * i;
                        LFS.GetComponent<Renderer>().material.mainTexture = Resources.Load(directoryName + currentPos.ToString("D4")) as Texture2D;
                        LFD.GetComponent<Renderer>().material.mainTexture = Resources.Load(depth_directoryName + currentPos.ToString("D4")) as Texture2D;
                        //   originalPosition.x = originalPosition.x - 0.2f * i;
                        //            originalPosition.y = originalPosition.y - 0.2f * j;
                        transform.localPosition = originalPosition;
                        file_index++;
                    }

                }
            }
        }
        */

        sw_ReadImg.Stop();
 //       UnityEngine.Debug.Log("LoadLF : " + sw_ReadImg.ElapsedTicks / 10 + " us");
   //     Debug.Log("mousePointer:" + Input.mousePosition.x);
    }

    public void LFS_setup(GameObject LFS_temp,string object_name,int current_pos)
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
